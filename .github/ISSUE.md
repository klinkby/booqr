# Scale-out safe scheduled background services

## Problem

`ScheduledBackgroundService` subclasses (`ReminderMailService`, `FlushTokenService`) use `Task.Delay` to fire at a configured time of day. When running multiple API instances, each instance independently triggers the same scheduled task, causing:

- **ReminderMailService**: Duplicate reminder emails sent to customers (one per instance)
- **FlushTokenService**: Redundant DELETE queries (harmless but wasteful)

## Design: Job execution claim table

Use a PostgreSQL table with a `(job_name, execution_date)` unique constraint as a distributed mutex. Before executing, each instance attempts an `INSERT ... ON CONFLICT DO NOTHING`. Only the instance whose INSERT succeeds (1 affected row) runs the task; all others skip.

This handles clock skew of any duration because the claim is based on date, not lock duration.

## Implementation plan

### 1. Schema — add `scheduled_job_executions` table

In `redist/initdb.sql`, add:

```sql
create table public.scheduled_job_executions
(
    job_name       varchar(50)              not null,
    execution_date date                     not null,
    claimed_at     timestamp with time zone not null,
    primary key (job_name, execution_date)
);
```

### 2. Core — add `IJobClaim` interface

In `src/Klinkby.Booqr.Core/`, add an interface:

```csharp
public interface IJobClaim
{
    Task<bool> TryClaimAsync(string jobName, DateOnly executionDate, CancellationToken cancellation);
}
```

No external dependencies — fits Core layer rules.

### 3. Infrastructure — implement `JobClaimRepository`

In `src/Klinkby.Booqr.Infrastructure/Repositories/`, add a sealed repository:

```csharp
internal sealed class JobClaimRepository(IConnectionProvider connectionProvider) : IJobClaim
{
    public async Task<bool> TryClaimAsync(string jobName, DateOnly executionDate, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.ExecuteAsync(
            $"""
            INSERT INTO scheduled_job_executions (job_name, execution_date, claimed_at)
            VALUES ({jobName}, {executionDate}, now())
            ON CONFLICT DO NOTHING
            """, cancellation) == 1;
    }
}
```

Uses `IConnectionProvider` and Dapper.AOT string interpolation, consistent with existing repository patterns. Registered automatically via `ServiceScan.SourceGenerator`.

### 4. Application — guard in `ScheduledBackgroundService`

Modify `ScheduledBackgroundService` to accept `IServiceProvider` and a `JobName` property:

```csharp
internal abstract partial class ScheduledBackgroundService(
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    ILogger logger) : BackgroundService
{
    protected abstract TimeSpan TriggerTimeOfDay { get; }
    protected abstract string JobName { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // ... existing delay logic ...
            await ExecuteIfClaimedAsync(stoppingToken);
        }
    }

    private async Task ExecuteIfClaimedAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var jobClaim = scope.ServiceProvider.GetRequiredService<IJobClaim>();

        if (await jobClaim.TryClaimAsync(JobName, DateOnly.FromDateTime(Now), stoppingToken))
        {
            await ExecuteScheduledTaskAsync(stoppingToken);
        }
        else
        {
            _log.Skipped();
        }
    }

    protected abstract Task ExecuteScheduledTaskAsync(CancellationToken cancellation);
}
```

Each subclass provides its `JobName`:
- `FlushTokenService` → `"flush-tokens"`
- `ReminderMailService` → `"reminder-mail"`

Note: `ScheduledBackgroundService` is in Application which must not reference Infrastructure. It accesses `IJobClaim` (a Core interface) via `IServiceProvider`, which is already the pattern used by both services for scoped repositories.

### 5. Tests

#### Unit tests (Application.Tests)
- Verify `ExecuteScheduledTaskAsync` runs when `TryClaimAsync` returns `true`
- Verify task is skipped when `TryClaimAsync` returns `false`
- Mock `IJobClaim` with Moq, use `FakeTimeProvider` for deterministic scheduling

#### Integration tests (Infrastructure.Tests)
- First call to `TryClaimAsync("test-job", today)` returns `true`
- Second call with same arguments returns `false`
- Different `execution_date` returns `true` again
- Use Testcontainers PostgreSQL, wrap in transaction + rollback

#### Architecture tests (Klinkby.Booqr.Tests)
- `IJobClaim` lives in Core assembly
- `JobClaimRepository` is sealed and lives in Infrastructure assembly
- No new architecture violations

### 6. Cleanup consideration

The table will accumulate one row per job per day. Add a retention cleanup (e.g., DELETE rows older than 30 days) to `FlushTokenService` since it already runs daily cleanup. This keeps it simple — no new service needed.

## Files to modify

| File | Change |
|------|--------|
| `redist/initdb.sql` | Add `scheduled_job_executions` table |
| `src/Klinkby.Booqr.Core/IJobClaim.cs` | New interface |
| `src/Klinkby.Booqr.Infrastructure/Repositories/JobClaimRepository.cs` | New repository |
| `src/Klinkby.Booqr.Application/Services/ScheduledBackgroundService.cs` | Add claim guard |
| `src/Klinkby.Booqr.Application/Services/FlushTokenService.cs` | Add `JobName`, cleanup old claims |
| `src/Klinkby.Booqr.Application/Services/ReminderMailService.cs` | Add `JobName` |
| `tests/Klinkby.Booqr.Application.Tests/Services/ScheduledBackgroundServiceTests.cs` | New/updated tests |
| `tests/Klinkby.Booqr.Infrastructure.Tests/Repositories/JobClaimRepositoryTests.cs` | New integration tests |

## Out of scope

- Channel-based services (`ActivityBackgroundService`, `EmailBackgroundService`) are instance-local by design and do not need coordination
- No external dependencies (Hangfire, Quartz, etc.) — PostgreSQL is sufficient
- No `completed_at` column or retry logic — both jobs tolerate a rare missed execution (flush runs tomorrow, reminders are best-effort)
