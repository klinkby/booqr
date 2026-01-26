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
    ILogger<DeleteBookingCommand> logger) : ICommand
{
    public async Task<bool> Execute(DeleteBookingRequest request, CancellationToken ct)
    {
        // 1. Authorize
        if (!IsAuthorized(request.User, booking.CustomerId))
            throw new UnauthorizedAccessException();

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

### Authorization Patterns
- **Customers**: Only access their own resources (`user.Id == targetUserId`)
- **Employees/Admins**: Access any resources
- Check authorization BEFORE calling repositories
- Throw `UnauthorizedAccessException` on access denied

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
