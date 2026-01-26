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

### Repository Interfaces
- `IRepository` - Base repository contract
- Specific repository interfaces (e.g., `IUserRepository`, `IBookingRepository`)

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
