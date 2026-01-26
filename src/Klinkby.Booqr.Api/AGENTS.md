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

### Example Structure
```csharp
app.MapPost("/api/bookings", async (
    CreateBookingRequest request,
    ICreateBookingCommand command,
    CancellationToken ct) =>
{
    var booking = await command.Execute(request, ct);
    return Results.Created($"/api/bookings/{booking.Id}", booking);
})
.RequireAuthorization("Customer")
.WithName("CreateBooking")
.WithOpenApi();
```

### Key Patterns
- Minimal route handlers with dependency injection
- Authorization policies on endpoints
- Cancellation token support
- Typed responses (Results.Ok, Results.Created, Results.NotFound, etc.)
- OpenAPI metadata via `.WithOpenApi()`

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
