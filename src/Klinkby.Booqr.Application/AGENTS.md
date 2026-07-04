# Application Layer (`Klinkby.Booqr.Application`)

## Purpose

The Application layer contains the business logic and orchestrates operations between the API and Infrastructure layers. It implements use cases through Commands and Services while remaining independent of I/O concerns.

## Architectural Rules

### Dependencies
- **Internal References**: Only references `Core` internally
- **MUST NOT reference `Infrastructure`**: Application layer is I/O agnostic
- **Third-party libraries allowed**: Limited to business logic concerns (BCrypt, JWT, DI abstractions)

### I/O Restrictions
**No direct I/O or data-access dependencies**. Must NOT depend on types in namespaces matching:
- ❌ `Dapper`
- ❌ `System.Console`
- ❌ `System.IO`
- ❌ `System.Net`
- ❌ `System.Data`
- ❌ `Npgsql`

### Immutability
- **Classes whose names end with `Request` are immutable**
- Use `record` types for request DTOs

### Contents
- **Commands**: ICommand implementations (use case orchestration)
- **Services**: Business logic services (e.g., token generation, password hashing)
- **Background services**: Hosted services for async processing (email, reminders, activity recording)
- **Request/Response DTOs**: Data transfer objects

### Purpose
- Use-case orchestration
- Business rules enforcement
- Transaction coordination
- Authorization logic

## Key Patterns

### Commands (ICommand)
Commands encapsulate use cases and orchestrate:
- Authorization checks (validate user access)
- Transaction management (Begin → Commit/Rollback)
- Repository interactions (via interfaces from Core)
- Business rule validation

Example structure:
```csharp
public sealed class DeleteBookingCommand(
    IBookingRepository bookings,
    ICalendarRepository calendar,
    ITransaction transaction,
    ILogger<DeleteBookingCommand> logger) : ICommand<DeleteBookingRequest, Task<Result<bool>>>
{
    public async Task<Result<bool>> Execute(DeleteBookingRequest request, CancellationToken ct)
    {
        // 1. Authorize
        if (!IsAuthorized(request.User, booking.CustomerId))
            return Problem.Forbidden with { Detail = "You do not have access to delete this booking" };

        // 2. Begin transaction
        await transaction.Begin(IsolationLevel.ReadCommitted, ct);

        try
        {
            // 3. Business logic
            var deleted = await bookings.Delete(id, ct);

            // 4. Side effects
            await calendar.Add(vacancy, ct);

            // 5. Commit
            await transaction.Commit(ct);
            return deleted;
        }
        catch
        {
            await transaction.Rollback(ct);
            throw;
        }
    }
}
```

### Result/Problem Pattern

Commands report expected failures (not found, forbidden, unauthorized, validation, conflict) as data, not exceptions. `Execute` returns `Task<Result<T>>` (or `Task<Result<bool>>` for delete/update) instead of throwing.

- **`Result<T>`** (`Result.cs`) is a closed discriminated union: `Result<T>.Success(T Value)` or `Result<T>.Fault(Problem Problem)`. `Result` (non-generic) is the `bool`-less equivalent used where there's no payload.
- **`Problem`** (`Problem.cs`) is an RFC 7807-shaped record (`Type`, `Title`, `HttpStatusCode`, `Detail`). Reuse the static instances (`Problem.NotFound`, `.ValidationFailed`, `.Unauthorized`, `.Forbidden`, `.Conflict`, `.MidAirCollision`) and customize with `with { Detail = "..." }`.
- **Implicit conversions** remove boilerplate: `return someValue;` becomes `Success`, and `return Problem.NotFound with { ... };` becomes `Fault` — no need to construct `new Result<T>.Success(...)` explicitly.
- **API layer mapping**: `CommandExtensions` (`Klinkby.Booqr.Api/Util/CommandExtensions.cs`) pattern-matches `Result<T>` and calls `Problem.ToProblemHttpResult()` on `Fault`, turning it into a `ProblemHttpResult` via `TypedResults.Problem`. Endpoints never see raw exceptions for these cases.
- Prefer returning a `Problem` over throwing whenever the failure is an expected outcome of the use case (not found, access denied, validation failed, optimistic-concurrency conflict, business-rule violation the caller can act on).

### Authorization Patterns
- **Customers**: Only access their own resources (`user.Id == targetUserId`)
- **Employees/Admins**: Access any resources
- Check authorization BEFORE calling repositories
- Return `Problem.Forbidden` (already-authenticated user acting on a resource they don't own) or `Problem.Unauthorized` (failed/missing authentication) instead of throwing — see `Result/Problem Pattern` above

### When to Still Throw

Exceptions are reserved for truly exceptional, non-recoverable conditions the caller isn't expected to handle as a business outcome:
- Programming/contract violations: `ArgumentNullException.ThrowIfNull(query)`, missing/invalid auth claims (`InvalidClaimException`)
- Invariant violations that indicate a bug: `UnreachableException` for exhaustive switches, `InvalidOperationException` when a just-created entity can't be re-read
- Anything genuinely unrecoverable at the use-case level (let it propagate to `StatusCode.FromException` in the API layer for a 5xx/502/504 mapping)

Do not throw for conditions a caller can reasonably branch on (not found, forbidden, validation, conflict) — model those as a `Problem` instead.

### Background Services
- **EmailWorker**: Processes email queue via channels
- **ActivityRecorder**: Records audit events asynchronously
- **ReminderService**: CRON-scheduled reminder delivery

## Testing Guidelines

See `tests/Klinkby.Booqr.Application.Tests/Commands/AGENTS.md` for detailed testing practices.

**Key principles**:
- Mock repositories/services with Moq
- Use `NullLogger<T>.Instance` (never mock ILogger)
- Accept `DateTime t0` from `[ApplicationAutoData]` for deterministic time
- Verify transaction lifecycle: `Begin` → `Commit` on success, `Rollback` on exception
- Assert repository calls are skipped (`Times.Never`) when unauthorized
- Assert on the `Result<T>` shape (`.IsSuccess`, `Result<T>.Fault { Problem: ... }`) rather than expecting exceptions for expected-failure paths

## Key Dependencies

From `.csproj`:
- **BCrypt.Net-Next**: Secure password hashing
- **System.IdentityModel.Tokens.Jwt**: JWT token generation
- **ServiceScan.SourceGenerator**: Automatic DI registration
- **Microsoft.Extensions.*** : Logging, DI, Configuration, Options, Hosting abstractions

## Enforcement

These rules are enforced through:
1. **Automated tests**: `Klinkby.Booqr.Tests` uses `TngTech.ArchUnitNET` to validate:
   - No `Infrastructure` references
   - No forbidden I/O namespace dependencies
   - `*Request` types are immutable
2. **Code review**: Manual verification during PR review

## Related Documentation

- **[ARCHITECTURE.md](../../ARCHITECTURE.md)** - Complete architectural policies
- **[tests/AGENTS.md](../../tests/AGENTS.md)** - General testing guidelines
- **[tests/Klinkby.Booqr.Application.Tests/Commands/AGENTS.md](../../tests/Klinkby.Booqr.Application.Tests/Commands/AGENTS.md)** - ICommand testing guidelines
- **[src/Klinkby.Booqr.Core/AGENTS.md](../Klinkby.Booqr.Core/AGENTS.md)** - Core layer guidelines
