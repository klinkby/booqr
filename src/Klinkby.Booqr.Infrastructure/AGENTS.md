# Infrastructure Layer (`Klinkby.Booqr.Infrastructure`)

## Purpose

The Infrastructure layer implements technical concerns like database access and external service integration. It provides concrete implementations of repository and service interfaces defined in the Core layer.

## Architectural Rules

### Dependencies
- **Internal References**: Only references `Core` internally
- **MUST NOT reference `Application`**: Infrastructure is unaware of business logic
- **Third-party I/O libraries allowed**: Database drivers, HTTP clients, etc.

### Repository Rules
- **Any class implementing `IRepository` must live in the Infrastructure assembly**
- **All repositories must be `sealed`**
- **Location**: Place in `Repositories/` subdirectory

### Business Logic Restrictions
- ❌ **No business logic like Commands**
- ❌ **No authorization logic**
- ❌ **No transaction orchestration** (transaction interface implementation only)

### Contents
- **Repositories**: Database access implementations (sealed classes)
- **I/O service agents**: Email clients, HTTP clients
- **Proxies**: External service integrations
- **Migrations**: Database schema management

## Key Technologies

### Dapper.AOT
- **Compile-time SQL query generation** with interceptors
- **String interpolation required** for AOT interceptor:
  ```csharp
  await connection.QuerySingleOrDefaultAsync<Booking>($"{GetByIdQuery}", new GetByIdParameters(id));
  ```
- Zero reflection, full AOT compatibility

### Custom Source Generator
- **Klinkby.Booqr.Infrastructure.Generators**: Query builder helpers
- See `src/Klinkby.Booqr.Infrastructure/Repositories/AGENTS.md` for Dapper.AOT usage

### Database
- **Npgsql 10**: PostgreSQL driver with async/streaming support
- **Connection pooling**: Via `Npgsql.DependencyInjection`
- **Schema bootstrapping**: Automatic on startup

## Repository Pattern

### Example Structure
```csharp
public sealed class BookingRepository(NpgsqlDataSource dataSource) : IBookingRepository
{
    private const string GetByIdQuery = """
        SELECT id, customer_id, service_id, start_time, end_time, created, modified, version, deleted
        FROM bookings
        WHERE id = @id AND deleted IS NULL
        """;

    public async Task<Booking?> GetById(int id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Booking>(
            $"{GetByIdQuery}",
            new GetByIdParameters(id));
    }

    // Other methods...
}
```

### Key Patterns
- Inject `NpgsqlDataSource` for connection management
- Use `await using` for automatic connection disposal
- Const strings for SQL queries (enables Dapper.AOT interception)
- Parameter objects for type safety
- Return `null` for not-found, not exceptions
- Respect soft-delete (`deleted IS NULL` in queries)

## Testing Guidelines

See `tests/Klinkby.Booqr.Infrastructure.Tests/Repositories/AGENTS.md` for detailed testing practices.

**Key principles**:
- **Integration tests** against real PostgreSQL via Testcontainers
- Resolve dependencies via `ServiceProviderFixture` DI container
- Wrap tests in explicit transaction, rollback in `finally`
- Use `[IntegrationAutoData]` for deterministic data
- Create related entities via repositories (not raw SQL)
- Assert `PostgresException.ConstraintName` for constraint violations

## Key Dependencies

From `.csproj`:
- **Dapper + Dapper.AOT**: High-performance data access with compile-time generation
- **Npgsql.DependencyInjection**: PostgreSQL driver with DI support
- **Klinkby.Booqr.Infrastructure.Generators**: Custom query builder source generator
- **Microsoft.Extensions.Http.Resilience**: Resilient HTTP clients with retry policies
- **ServiceScan.SourceGenerator**: Automatic DI registration

## External Services

### EmailLabs Mail Client
- HTTP-based email delivery
- Implements `IMailClient` from Core
- Uses resilient HTTP client with retry policies
- Templates stored as embedded Handlebars resources

## Enforcement

These rules are enforced through:
1. **Automated tests**: `Klinkby.Booqr.Tests` uses `TngTech.ArchUnitNET` to validate:
   - No `Application` references
   - Repository implementations are `sealed`
   - Repositories live in Infrastructure assembly
2. **Code review**: Manual verification during PR review

## Related Documentation

- **[ARCHITECTURE.md](../../ARCHITECTURE.md)** - Complete architectural policies
- **[tests/AGENTS.md](../../tests/AGENTS.md)** - General testing guidelines
- **[tests/Klinkby.Booqr.Infrastructure.Tests/Repositories/AGENTS.md](../../tests/Klinkby.Booqr.Infrastructure.Tests/Repositories/AGENTS.md)** - Repository testing guidelines
- **[src/Klinkby.Booqr.Core/AGENTS.md](../Klinkby.Booqr.Core/AGENTS.md)** - Core layer guidelines
- **[src/Klinkby.Booqr.Infrastructure/Repositories/AGENTS.md](Repositories/AGENTS.md)** - Dapper.AOT usage notes
