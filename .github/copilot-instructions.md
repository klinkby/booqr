# Copilot Instructions for Klinkby.Booqr

## Repository Overview

This is an AOT-enabled ASP.NET 10 Web API for booking management with PostgreSQL database.
The service follows a minimalist clean architecture emphasizing performance and maintainability.

**License**: AGPL-3.0

## Project Structure

```
src/
├── Klinkby.Booqr.Core         # Domain models, interfaces, core utilities
├── Klinkby.Booqr.Application  # Business logic, commands, services
├── Klinkby.Booqr.Infrastructure  # Data access (PostgreSQL), external integrations
└── Klinkby.Booqr.Api          # Entry point, HTTP middleware, routing
tests/
├── Klinkby.Booqr.Api.Tests
├── Klinkby.Booqr.Application.Tests
└── Klinkby.Booqr.Infrastructure.Tests
```

## Build and Test Commands

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal
```

For vulnerability scanning:
```bash
dotnet list package --vulnerable
```

## Coding Standards

### Language and Framework
- .NET 10 with Native AOT compilation
- C# with nullable reference types enabled (`<Nullable>enable</Nullable>`)
- File-scoped namespaces
- Use `var` for built-in types and when type is apparent
- 4-space indentation

### Source Generators (Required for AOT)
The project uses compile-time code generation extensively:
- ServiceScan for DI registration
- LoggerMessage for high-performance logging
- System.Text.Json source generation for serialization
- OptionsValidator for compile-time options validation
- Dapper.AOT for ORM mapping

### Code Style
- Prefer async/await with `CancellationToken` parameters
- Use `IAsyncEnumerable<T>` for streaming collection endpoints
- Use expression-bodied members for properties and indexers
- Prefer primary constructors when appropriate
- All public interfaces should have XML documentation comments

### Testing
- Use xUnit for unit testing
- Test naming convention: `GIVEN_Condition_WHEN_Action_THEN_ExpectedResult` or `GIVEN_Condition_THEN_Result`
- Integration tests use Testcontainers to spin up PostgreSQL instances

## Security Guidelines

**Critical**: Review the [SECURITY.md](../SECURITY.md) file for full security policy.

- Never commit secrets or credentials
- Never disable security settings in `nuget.config` or `.github/dependabot.yml`
- Run `dotnet list package --vulnerable` before submitting PRs
- Check for unexpected changes in `packages.lock.json` files
- Minimize dependencies to reduce attack surface
- This project uses a 7-day dependency cooldown period for supply chain protection

## Boundaries

Do not modify:
- `.github/dependabot.yml` security settings
- `nuget.config` package source mappings
- Files in `/certs/`, `/dpkeys/`, `/w3clog/`

## Architecture Patterns

- Repository pattern for data access (`IRepository<T>`, `IImmutableRepository<T, TKey>`)
- Soft delete pattern (set `Deleted` field instead of removing records)
- Use `ITransaction` for database transactions
- Background services for deferred processing (email, activity recording, reminders)
- Channels for async pipelines with immediate response
