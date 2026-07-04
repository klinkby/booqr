# API Layer (`Klinkby.Booqr.Api`)

## Purpose

The API layer is the HTTP presentation layer, exposing the application via REST endpoints using ASP.NET Core Minimal APIs. It handles HTTP concerns, authentication, authorization, and request/response serialization.

## Architectural Rules

### Dependencies
- **No specific dependency restrictions**: Can reference Application, Infrastructure, and Core
- **Dependency injection**: All dependencies resolved via DI container

### Business Logic Restrictions
- ❌ **No actual business logic**
- ❌ **No direct database access** (use Commands from Application layer)
- ✅ **Only HTTP presentation concerns**

### Contents
- **Minimal API endpoints**: HTTP route handlers
- **Middleware configuration**: Authentication, authorization, logging, error handling
- **OpenAPI/Swagger**: Build-time generated specification
- **Configuration**: appsettings, dependency injection setup

## Key Technologies

### ASP.NET Core Minimal APIs
- No controllers, only route handlers
- Functional programming style
- Source-generated OpenAPI spec

### AOT Compilation
- **Native AOT** with aggressive trimming
- Optimized for startup time and memory usage
- See `.csproj` for AOT settings:
  - `PublishAot=true`
  - Aggressive trimming flags (no debugger, no UTF7, invariant globalization)
  - `OptimizationPreference=Speed`

### Authentication & Authorization

#### JWT Bearer Authentication
- Access tokens for API authentication
- Short-lived (configurable expiration)
- Role-based authorization policies

#### Refresh Token Rotation
- **Opaque refresh tokens** with 240-bit cryptographic entropy
- **HttpOnly cookies**: Secure storage with `HttpOnly`, `Secure`, `SameSite=Strict`, path-scoped attributes
- **Family-based tracking**: Detects token reuse attacks
- **SHAKE128 hashing**: For secure database storage
- **Automatic revocation**: On compromise detection
- **Transactional rotation**: Atomic token swap during refresh
- **Daily cleanup**: Background service removes expired tokens

#### Security Features
- BCrypt password hashing with timing attack mitigation
- Email verification for account activation
- Token family revocation on logout

### OpenAPI
- **Build-time generated** specification (zero runtime overhead)
- Served from `wwwroot/openapi/v1.json`
- Configuration in `.csproj`:
  ```xml
  <OpenApiDocumentsDirectory>./wwwroot/openapi</OpenApiDocumentsDirectory>
  <OpenApiGenerateDocumentsOptions>--file-name v1</OpenApiGenerateDocumentsOptions>
  ```

### Logging
- **NLog** with CLEF (Compact Log Event Format)
- Structured JSON logging
- Configured via `nlog.config`

### Error Handling
- **Problem Details** (RFC 7807) for structured error responses
- Detailed validation errors
- Consistent error format across all endpoints

## Endpoint Patterns

All routes are mapped in `Routing.cs`, grouped under `/api` via `MapApiRoutes`. The base
route group carries endpoint filters applied to every request (`RequestMetadataEndPointFilter`
for ETag handling, `AuthenticatedRequestEndPointFilter` to inject the authenticated
`ClaimsPrincipal` into `IAuthenticatedRequest` arguments before the handler runs — see below).

### Example Structure
```csharp
group.MapPost("",
        static (AddBookingCommand command,
                [FromBody] AddBookingRequest request,
                ClaimsPrincipal user, CancellationToken cancellation) => command
            .Execute(request, cancellation)
            .ToCreated(resourceName))
    .RequireAuthorization(UserRole.Customer)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status201Created)
    .WithName("addBooking")
    .WithSummary("Add a booking");
```

Command dispatch is a chain: the endpoint lambda binds the request (`[FromBody]` or
`[AsParameters]`), resolves the `ICommand` from DI, and calls `command.Execute(request, ct)`,
which returns `Task<Result<T>>`. That task is piped directly into a `CommandExtensions`
(`Util/CommandExtensions.cs`) mapping extension — `.ToOk()`, `.ToCreated(resourceName)`, or
`.ToNoContent()` — which pattern-matches `Result<T>.Success`/`Result<T>.Fault` and produces the
typed HTTP result (`TypedResults.Ok`/`Created`/`NoContent` on success, `TypedResults.Problem`
via `MapFault` on fault). Handlers never construct `IResult`s directly or branch on success/failure
themselves; that mapping lives entirely in `CommandExtensions`.

### Authenticated user injection
Request records that implement `IAuthenticatedRequest` (via the `AuthenticatedRequest` base
record, e.g. `AddBookingRequest`, `AuthenticatedByIdRequest`) get their `User` property set
**automatically** by `AuthenticatedRequestEndPointFilter` (`Filters/AuthenticatedRequestEndPointFilter.cs`),
which runs on every `/api` request before the handler. The filter scans the bound endpoint
arguments for an `IAuthenticatedRequest` and assigns `context.HttpContext.User` to it. Endpoint
lambdas therefore do **not** need to (and should not) do `request with { User = user }` — just
pass the bound `request` straight to `command.Execute(...)`. A `ClaimsPrincipal user` parameter
may still be bound separately when a handler needs the principal directly outside of `Execute`
(e.g. redirect construction), but it no longer needs to be spliced into the request.

### Key Patterns
- Minimal route handlers with dependency injection; `static` lambdas to avoid closures
- Request binding via `[FromBody]` (body) or `[AsParameters]` (route/query); `Id` set
  post-binding via `request with { Id = id }` where needed, `User` is injected automatically
  (see above, do not set it manually)
- Authorization policies on endpoints (`.RequireAuthorization(UserRole.X)`)
- Cancellation token support
- Command result chained straight into a `CommandExtensions` mapper (`ToOk`/`ToCreated`/`ToNoContent`)
  for typed, `Result<T>`-aware HTTP responses — no manual `Results.*` construction
- Cross-cutting concerns (ETag, authenticated-user injection) live in endpoint filters on the
  base route group, not in individual handlers

## Configuration

### appsettings.json
- Database connection strings
- JWT settings (issuer, audience, secret key)
- Email service configuration
- Logging levels

### User Secrets (Development)
- `UserSecretsId`: dcde7d5b-2077-4409-b78a-c1253e20c40f
- Store sensitive config (passwords, API keys) locally during development

## Key Dependencies

From `.csproj`:
- **Microsoft.AspNetCore.Authentication.JwtBearer**: JWT authentication
- **Microsoft.AspNetCore.OpenApi**: Build-time OpenAPI generation
- **NLog + NLog.Web.AspNetCore**: Structured logging
- **Application + Infrastructure**: Business logic and data access

## Deployment

### Docker
- Alpine Linux base image (~17MB)
- Rootless execution
- Immutable filesystem
- UNIX sockets for inter-container communication

### Environment
- PostgreSQL backend via Docker Compose
- HAProxy gateway in front
- Health checks and graceful shutdown

## Testing

See `tests/Klinkby.Booqr.Api.Tests/` for endpoint integration tests.

**Key approaches**:
- `WebApplicationFactory` for in-memory testing
- Test authentication/authorization flows
- Validate response formats and status codes
- Test error handling and validation

## Enforcement

These rules are enforced through:
1. **Code review**: Manual verification during PR review
2. **Integration tests**: Validate endpoint behavior and security

## Related Documentation

- **[ARCHITECTURE.md](../../ARCHITECTURE.md)** - Complete architectural policies
- **[SECURITY.md](../../SECURITY.md)** - Security practices and supply chain defense
- **[tests/AGENTS.md](../../tests/AGENTS.md)** - General testing guidelines
- **[tests/Klinkby.Booqr.Api.Tests/AGENTS.md](../../tests/Klinkby.Booqr.Api.Tests/AGENTS.md)** - API testing guidelines
- **[src/Klinkby.Booqr.Application/AGENTS.md](../Klinkby.Booqr.Application/AGENTS.md)** - Business logic layer
