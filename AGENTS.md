# Klinkby.Booqr - AI Agent Context

> This repo use only AGENTS.md files for AI instructions.

## Summary

Klinkby.Booqr is an AOT-compiled ASP.NET 10 booking management API built on minimalist clean architecture with vertical feature slices.
The system emphasizes performance, security, and maintainability through extensive compile-time code generation (source generators for DI, logging, JSON, ORM, OpenAPI), Native AOT with aggressive trimming, and strict architectural boundaries enforced by automated tests.

**Multi-tenant:** many businesses share one PostgreSQL database, isolated by **`FORCE ROW LEVEL SECURITY`** in a shared `app` schema plus **one login role per tenant** (`t_<id>`). The RLS policy is keyed on the connected role (`current_user` → `app.tenant_of()`), so isolation is DB-enforced and unforgeable — no per-request session variable. The API resolves the tenant from the request host (`<slug>.booqr.dk`) and connects as that tenant's role; the `Klinkby.Booqr.Control` admin CLI provisions tenants/roles. See `docs/1-design.md` for the full design.

## Repository Structure
- **Klinkby.Booqr.slnx** - Use the new XML-based solution file
- **src/**
  - **Klinkby.Booqr.Core/** - Domain contracts, records, interfaces
  - **Klinkby.Booqr.Application/** - Business logic, Commands, Services
  - **Klinkby.Booqr.Infrastructure/** - Repositories, I/O agents, Dapper queries; also the tenant/registry/batch data sources, `SchemaMigrator`, and `Migrations/*.sql` (the migrator-owned `app` schema)
  - **Klinkby.Booqr.Api/** - HTTP endpoints, Minimal API, JWT auth, tenant-resolution middleware; branches into admin mode on a leading `admin` arg
  - **Klinkby.Booqr.Control/** - Control-plane admin CLI (provision/migrate/deprovision/rotate). References Infrastructure + Core; **Application and Infrastructure must NOT depend on it** (ArchUnit-enforced) so the elevated `booqr_migrator`/`booqr_batch` provisioning logic never runs on a request path.
- **redist/**
  - **initdb/** - one-time control-plane bootstrap (schemas, `public.tenants`, roles, `app.tenant_of()`); run by the Postgres container as superuser
  - **docker-compose.yml** - HAProxy, api, postgres, and the internal-only `admin` service
  - **web-gateway/haproxy.cfg** - routes `*.booqr.dk`; apex→www redirect
- **tests/**
  - **Klinkby.Booqr.Tests/** - ArchUnit architectural policy tests
  - **Klinkby.Booqr.Core.Tests/** - Core domain unit tests
  - **Klinkby.Booqr.Application.Tests/** - ICommand unit tests with Moq
  - **Klinkby.Booqr.Infrastructure.Tests/** - Repository integration tests with Testcontainers
  - **Klinkby.Booqr.Api.Tests/** - API endpoint integration tests
- **ARCHITECTURE.md** - Detailed layer policies
- **SECURITY.md** - Supply chain and security practices
- **AGENTS.md** - This file

## Architecture: Clean Architecture with Vertical Slices

The solution follows a minimalist clean architecture with four distinct layers, each with strict dependency rules enforced by automated tests.

### Layers

- **[Core](src/Klinkby.Booqr.Core/AGENTS.md)** - Domain contracts, immutable records, interfaces (no third-party dependencies)
- **[Application](src/Klinkby.Booqr.Application/AGENTS.md)** - Business logic, Commands, Services (references Core only, no I/O)
- **[Infrastructure](src/Klinkby.Booqr.Infrastructure/AGENTS.md)** - Repositories, database access, external services (references Core only)
- **[API](src/Klinkby.Booqr.Api/AGENTS.md)** - HTTP endpoints, authentication, Minimal APIs (references all layers)
- **Control** (`src/Klinkby.Booqr.Control/`) - Admin CLI over Infrastructure + Core. Off every request-path assembly (Application/Infrastructure must not depend on it).

Each layer has detailed guidelines in its respective `AGENTS.md` file. Click the links above for layer-specific architectural rules, patterns, and examples.

### Enforcement

Architectural policies are validated automatically via `TngTech.ArchUnitNET` tests in `Klinkby.Booqr.Tests`.

## Key Technologies and Tools

### Compile-time Code Generation (Source Generators)

- **ServiceScan.SourceGenerator**: Automatic dependency injection registration
- **Dapper.AOT**: AOT-compatible ORM with compile-time SQL query generation
- **Klinkby.Booqr.Infrastructure.Generators**: Custom query builder helpers
- **LoggerMessage**: High-performance structured logging
- **System.Text.Json (source-generated)**: Zero-reflection JSON serialization
- **OptionsValidator**: Compile-time configuration validation
- **Chorn.EmbeddedResourceAccessGenerator**: Compile-time embedded resource access

### Runtime Stack

- **.NET 10**: Native AOT with aggressive trimming (~17MB Alpine Linux images)
- **Npgsql 10**: PostgreSQL driver with async/streaming support
- **BCrypt.Net-Next**: Secure password hashing with timing attack mitigation
- **NLog**: CLEF (Compact Log Event Format) structured logging
- **Microsoft.AspNetCore.Authentication.JwtBearer**: JWT + opaque refresh tokens

### Security

- **Tenant isolation**: `FORCE ROW LEVEL SECURITY` on every `app` table, policy `tenant_id = app.tenant_of(current_user)`; per-tenant `NOBYPASSRLS` login roles (`t_<id>`) with passwords derived as `base64url(HMAC-SHA256(master_secret, id))`. Cross-tenant background jobs use a separate `booqr_batch` (BYPASSRLS) role via `IBatchScope`. The `tenant` JWT claim must match the host tenant (403 otherwise); DB RLS is the backstop.
- **Refresh token rotation**: 240-bit entropy, family-based reuse detection, SHAKE128 hashing
- **HttpOnly cookies**: Secure, SameSite=Strict, path-scoped
- **Supply chain defense**: 7-day Dependabot cooldown, package source mapping, lock files
- **Constraints**: Database-enforced referential integrity, check constraints, unique indexes

### Testing

- **Microsoft.Testing.Platform (MTP)**: Native .NET 10 test runner (configured via `global.json`)
- **xUnit v3**: Test framework (with MTP v2 integration via `xunit.v3.mtp-v2`)
- **Microsoft.Testing.Extensions.CodeCoverage**: MTP-native code coverage collection
- **Moq**: Behavior verification for Application layer unit tests
- **AutoFixture**: Data generation via `[AutoData]` and `[IntegrationAutoData]`
- **Testcontainers**: Real PostgreSQL instance for Infrastructure integration tests
- **Microsoft.Extensions.Time.Testing (FakeTimeProvider)**: Deterministic time in tests
- **TngTech.ArchUnitNET**: Architectural policy enforcement

### DevOps

- **Docker Compose**: Multi-container orchestration (HAProxy, API, PostgreSQL)
- **Alpine Linux**: Minimal container images, rootless execution, immutable filesystem
- **GitHub Actions**: CI/CD with Docker image builds, CodeQL, Codecov
- **UNIX sockets**: Efficient inter-container communication

## Testing Guidelines

See **[tests/AGENTS.md](tests/AGENTS.md)** for comprehensive testing practices and patterns used across all test projects.

### Test Projects

- **[Klinkby.Booqr.Tests](tests/Klinkby.Booqr.Tests/AGENTS.md)** - Architecture policy enforcement with ArchUnitNET
- **[Klinkby.Booqr.Core.Tests](tests/Klinkby.Booqr.Core.Tests/AGENTS.md)** - Domain model unit tests
- **[Klinkby.Booqr.Application.Tests](tests/Klinkby.Booqr.Application.Tests/Commands/AGENTS.md)** - Business logic unit tests with mocks
- **[Klinkby.Booqr.Infrastructure.Tests](tests/Klinkby.Booqr.Infrastructure.Tests/Repositories/AGENTS.md)** - Database integration tests with Testcontainers
- **[Klinkby.Booqr.Api.Tests](tests/Klinkby.Booqr.Api.Tests/AGENTS.md)** - HTTP endpoint integration tests with WebApplicationFactory

## Related Documentation

- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Full layer policies and dependency rules
- **[SECURITY.md](SECURITY.md)** - Supply chain defense, vulnerability reporting
- **[tests/AGENTS.md](tests/AGENTS.md)** - General testing guidelines
- **[README.md](README.md)** - Features, setup instructions, license (AGPL-3.0)

### Layer-Specific Documentation

- **[src/Klinkby.Booqr.Core/AGENTS.md](src/Klinkby.Booqr.Core/AGENTS.md)** - Core layer guidelines
- **[src/Klinkby.Booqr.Application/AGENTS.md](src/Klinkby.Booqr.Application/AGENTS.md)** - Application layer guidelines
- **[src/Klinkby.Booqr.Infrastructure/AGENTS.md](src/Klinkby.Booqr.Infrastructure/AGENTS.md)** - Infrastructure layer guidelines
- **[src/Klinkby.Booqr.Api/AGENTS.md](src/Klinkby.Booqr.Api/AGENTS.md)** - API layer guidelines

## Important Notes for AI Agents

1. **Dapper.AOT requirement**: SQL queries MUST use string interpolation for the interceptor to work:
   ```csharp
   await connection.QuerySingleOrDefaultAsync<Booking>($"{GetByIdQuery}", new GetByIdParameters(id));
   ```

2. **Layer boundaries**: Core/Application/Infrastructure separation is strict and enforced by tests. Violations will fail CI.

3. **Immutability**: Core types and Application `*Request` types must be immutable. Use `record` types.

4. **No ceremony**: Prefer concise, functional code. Avoid unnecessary abstractions.

5. **Test determinism**: Always use `t0` from AutoData for time, never `DateTime.UtcNow` in tests.

6. **Transactions in tests**: Infrastructure tests must rollback transactions to keep database clean.

7. **Authorization first**: Application commands must validate user access before calling repositories.

8. **Tenant isolation is DB-enforced — do not hand-roll it**: RLS + the `tenant_id DEFAULT app.tenant_of(current_user)` column scope every query automatically. **Never add `WHERE tenant_id = …`** to a query, and never write `tenant_id` on a normal tenant connection (the DEFAULT stamps it; `WITH CHECK` rejects other values). The only exception is the `booqr_batch` (BYPASSRLS) path, which sets `tenant_id` explicitly.

9. **`ITenantContext` and the tenant connection**: commands needing the current tenant inject Core's `ITenantContext` (populated by the API's tenant-resolution middleware). The scoped tenant `DbConnection` resolves **lazily on first use** and fails closed if no tenant — so keep DB access out of DI construction, and mark tenant-optional endpoints with `TenantOptionalAttribute`.

10. **Control-plane boundary**: put provisioning/migration logic in `Klinkby.Booqr.Control` only. Application and Infrastructure must not reference it (ArchUnit fails otherwise). New superuser-only DDL or `public.*` grants belong in `redist/initdb/*.sql`, not migrations.

11. **AOT-safe**: this is a Native-AOT app — avoid reflection-based `DataAnnotations` (e.g. `[Range(typeof(TimeSpan), …)]` → IL2026). Verify the real gate with a `dotnet publish` (AOT) + run, not just a build.
