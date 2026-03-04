# Core Layer (`Klinkby.Booqr.Core`)

## Purpose

The Core layer defines the domain contracts and data structures for the Klinkby.Booqr booking management system. It is the foundation of the clean architecture, containing only pure domain models with zero dependencies on external libraries or frameworks.

## Architectural Rules

### Dependencies
- **References**: Only `System.*` assemblies are allowed (no third-party libraries)
- **Internal references**: Types may reference other `Core` types
- **Zero external dependencies**: No NuGet packages, no external frameworks

### Immutability
- **All Core classes/types are immutable**
- Use `record` types for data structures
- No mutable state allowed

### Allowed Types
- **Records**: Immutable data structures (e.g., `User`, `Booking`, `Service`)
- **Interfaces**: Contracts for repositories and services (e.g., `IRepository`, `ITransaction`)
- **Exceptions**: Domain-specific exceptions
- **Static classes**: For constants only

### Forbidden
- ❌ Mutable classes
- ❌ Business logic (belongs in Application layer)
- ❌ I/O operations
- ❌ Third-party dependencies

## Key Types

### Base Record: `Audit`
All entity records derive from `Audit`, which provides lifecycle tracking:
- `Id` - Entity identifier
- `Created` - Creation timestamp
- `Modified` - Last modification timestamp
- `Version` - Optimistic concurrency version
- `Deleted` - Soft delete timestamp (nullable)

### Domain Records
Examples include:
- `User` - User account entity
- `Booking` - Booking entity
- `Service` - Service offering entity
- `Location` - Physical location entity
- `CalendarEvent` - Calendar/availability entity
- `EmployeeService` - Join entity (employee ↔ service assignment, no audit columns)

**Note on join-table entities**: Records that map many-to-many join tables without audit columns do **not** inherit `Audit`. They may expose a `static CompositeId(...)` method using `HashCode.Combine` when other layers need a single integer to represent the composite key (e.g. for activity recording):
```csharp
public sealed record EmployeeService(int EmployeeId, int ServiceId)
{
    public static int CompositeId(int employeeId, int serviceId) =>
        HashCode.Combine(employeeId, serviceId);
}
```

### Repository Interfaces
- `IRepository` - Base repository contract
- Specific repository interfaces (e.g., `IUserRepository`, `IBookingRepository`, `IEmployeeServiceRepository`)
- Join-table repositories extend `IRepository` directly with bespoke methods (no `IRepository<T, TKey>` since there is no single primary key)

### Infrastructure Interfaces
- `ITransaction` - Transaction management contract
- `IMailClient` - Email sending contract

## Enforcement

These rules are enforced through:
1. **Automated tests**: `Klinkby.Booqr.Tests` uses `TngTech.ArchUnitNET` to validate architectural policies
2. **Code review**: Manual verification during PR review
3. **Project references**: The `.csproj` file contains no third-party package references

## Related Documentation

- **[ARCHITECTURE.md](../../ARCHITECTURE.md)** - Complete architectural policies for all layers
- **[tests/Klinkby.Booqr.Core.Tests/AGENTS.md](../../tests/Klinkby.Booqr.Core.Tests/AGENTS.md)** - Core layer testing guidelines
- **[src/Klinkby.Booqr.Application/AGENTS.md](../Klinkby.Booqr.Application/AGENTS.md)** - Application layer guidelines
- **[src/Klinkby.Booqr.Infrastructure/AGENTS.md](../Klinkby.Booqr.Infrastructure/AGENTS.md)** - Infrastructure layer guidelines
