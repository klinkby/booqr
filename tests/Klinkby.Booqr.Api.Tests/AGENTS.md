# API Layer Tests (`Klinkby.Booqr.Api.Tests`)

## Purpose

Integration tests for the API layer, validating HTTP endpoints, authentication, authorization, request/response serialization, and error handling. These tests verify the complete HTTP request pipeline from endpoint to response.

All standard testing practices from [tests/AGENTS.md](../AGENTS.md) apply.

## Technology

- **WebApplicationFactory**: In-memory test server for ASP.NET Core
- **Testcontainers** (if needed): Real PostgreSQL for integration scenarios

## Test Scope

### What to Test

1. **Endpoint routing**: Correct HTTP methods and paths
2. **Authentication**: JWT bearer token validation
3. **Authorization**: Role-based access controcode sasyncl (Customer, Employee, Admin)
4. **Request validation**: Model validation and business rule enforcement
5. **Response formats**: JSON serialization, status codes, Problem Details
6. **Error handling**: Exception handling and error responses
7. **Security**: CORS, refresh token cookies, XSS/CSRF protection

### What NOT to Test

- ❌ **Business logic details** (tested in Application.Tests)
- ❌ **Database queries** (tested in Infrastructure.Tests)
- ❌ **Complex command logic** (tested in Application.Tests)

Focus on the HTTP boundary and integration between layers.

## Test Patterns

### Basic Endpoint Test with WebApplicationFactory

```csharp
public class BookingEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public BookingEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GIVEN_ValidRequest_WHEN_CreatingBooking_THEN_ReturnsCreated()
    {
        // Arrange
        var request = new CreateBookingRequest(
            ServiceId: 1,
            StartTime: DateTime.UtcNow.AddDays(1),
            EndTime: DateTime.UtcNow.AddDays(1).AddHours(1));

        var token = GenerateTestToken(userId: 1, role: "Customer");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/bookings", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);
        Assert.InRange(booking.Id, 1, int.MaxValue);
    }
}
```

### Testing Authentication

```csharp
[Fact]
public async Task GIVEN_NoToken_WHEN_AccessingProtectedEndpoint_THEN_ReturnsUnauthorized()
{
    // Arrange - no auth header

    // Act
    var response = await _client.GetAsync("/api/bookings/1");

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

### Testing Authorization

```csharp
[Fact]
public async Task GIVEN_CustomerToken_WHEN_AccessingAdminEndpoint_THEN_ReturnsForbidden()
{
    // Arrange
    var token = GenerateTestToken(userId: 1, role: "Customer");
    _client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    // Act
    var response = await _client.GetAsync("/api/admin/users");

    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

### Testing Validation Errors

```csharp
[Fact]
public async Task GIVEN_InvalidRequest_WHEN_CreatingBooking_THEN_ReturnsBadRequestWithProblemDetails()
{
    // Arrange
    var invalidRequest = new CreateBookingRequest(
        ServiceId: -1,  // Invalid
        StartTime: DateTime.UtcNow.AddDays(-1),  // In the past
        EndTime: DateTime.UtcNow.AddDays(-1));

    var token = GenerateTestToken(userId: 1, role: "Customer");
    _client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    // Act
    var response = await _client.PostAsJsonAsync("/api/bookings", invalidRequest);

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
    Assert.NotNull(problemDetails);
    Assert.Contains("ServiceId", problemDetails.Extensions.Keys);
}
```

### Testing Refresh Token Cookies

```csharp
[Fact]
public async Task GIVEN_ValidCredentials_WHEN_Login_THEN_SetsHttpOnlyRefreshTokenCookie()
{
    // Arrange
    var loginRequest = new LoginRequest("user@example.com", "password123");

    // Act
    var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
    var refreshTokenCookie = cookies.FirstOrDefault(c => c.StartsWith("refresh_token="));
    Assert.NotNull(refreshTokenCookie);
    Assert.Contains("HttpOnly", refreshTokenCookie);
    Assert.Contains("Secure", refreshTokenCookie);
    Assert.Contains("SameSite=Strict", refreshTokenCookie);
}
```

## Custom WebApplicationFactory

For more complex scenarios, customize the factory:

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace services for testing
            services.RemoveAll<IMailClient>();
            services.AddSingleton<IMailClient, MockMailClient>();

            // Use in-memory database or Testcontainers
        });

        builder.UseEnvironment("Test");
    }
}
```

## Helper Methods

### Token Generation

```csharp
private string GenerateTestToken(int userId, string role)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Role, role)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-32-chars-min!"));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: "test",
        audience: "test",
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

## Test Approach

- Use `IClassFixture<WebApplicationFactory<Program>>` for shared setup
- Test complete user workflows (signup → login → create booking → logout)
- Test error paths (invalid input, unauthorized access, not found)
- Test edge cases (expired tokens, concurrent requests)

## Coverage Goals

Focus on:
- ✅ All endpoint routes and HTTP methods
- ✅ Authentication/authorization for all protected endpoints
- ✅ Request validation for all inputs
- ✅ Error handling for common failure scenarios
- ✅ Security headers and cookie attributes

## Related Documentation

- **[tests/AGENTS.md](../AGENTS.md)** - General testing guidelines
- **[src/Klinkby.Booqr.Api/AGENTS.md](../../src/Klinkby.Booqr.Api/AGENTS.md)** - API layer architectural rules
- **SECURITY.md** (root) - Security practices to validate in tests
