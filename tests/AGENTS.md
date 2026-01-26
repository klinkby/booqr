# Testing Guidelines

This document contains general testing practices shared across all test projects in the Klinkby.Booqr solution.

## General Principles

### Naming Convention

**Pattern**: `GIVEN_{conditions}_WHEN_{action}_THEN_{outcome}`

- **GIVEN/WHEN/THEN**: Always uppercase
- **Insertions**: PascalCased
- **Example**: `GIVEN_CustomerUser_WHEN_AccessingOtherUsersBooking_THEN_ThrowsUnauthorizedException`

### Test Structure (AAA)

Partition tests into three clear sections:

1. **Arrange**: Set up test data, mocks, and preconditions
2. **Act**: Execute the operation being tested
3. **Assert**: Verify the outcome

Use blank lines to visually separate sections.

### Test Framework and Tools

- **xUnit v3**: Primary test execution framework across all projects
- **Moq**: Mocking framework for unit tests (mock collaborators narrowly, verify behavior precisely)
- **AutoFixture**: Automatic test data generation via `[AutoData]` and custom attributes (`[ApplicationAutoData]`, `[IntegrationAutoData]`)
- **Testcontainers**: Real PostgreSQL instances for integration tests
- **WebApplicationFactory**: In-memory ASP.NET Core server for API tests

## Common Patterns

### Use AutoFixture for Test Data

Prefer `[Theory]` with `[AutoData]` over hardcoded values:

```csharp
[Theory]
[AutoData]
public void TestMethod(User user, Service service, DateTime timestamp)
{
    // Test body uses generated values
}
```

**Benefits**:
- Eliminates magic numbers and hardcoded strings
- Tests with diverse data automatically
- Reduces test maintenance

### Mocking with Moq

```csharp
// Arrange
var mockRepository = new Mock<IBookingRepository>();
mockRepository
    .Setup(r => r.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedBooking);

// Act
var result = await sut.Execute(request, CancellationToken.None);

// Assert
mockRepository.Verify(
    r => r.GetById(expectedId, It.IsAny<CancellationToken>()),
    Times.Once);
```

**Best practices**:
- Mock interfaces, not concrete classes
- Verify critical interactions with `Times.Once`, `Times.Never`
- Use `It.IsAny<T>()` for parameters you don't care about
- Create factory methods for complex reusable mocks

### Logging in Tests

Use `NullLogger<T>.Instance` when log output verification is unnecessary:

```csharp
var command = new MyCommand(
    repository,
    NullLogger<MyCommand>.Instance);
```

**Never mock ILogger** - use the real null implementation.

### CancellationToken in Tests

Pass `CancellationToken.None` unless specifically testing cancellation:

```csharp
var result = await repository.GetById(id, CancellationToken.None);
```

### Timeouts for Async Tests

Use timeouts for hang-prone async methods:

```csharp
[Fact(Timeout = 5000)] // 5 seconds
public async Task TestMethod()
{
    // Test body
}
```

### Deterministic Time

**Never use `DateTime.UtcNow` directly in tests.** Always:

1. Accept `DateTime t0` from AutoData attributes
2. Compute all other times relative to `t0`
3. Use `FakeTimeProvider` when testing time-dependent logic

```csharp
[Theory]
[ApplicationAutoData]
public async Task TestMethod(DateTime t0, User user)
{
    var startTime = t0.AddDays(1);
    var endTime = t0.AddDays(1).AddHours(1);
    // Use computed times
}
```

## Test Project Organization

The solution contains five test projects, each with specific responsibilities:

### Test Projects

- **[Klinkby.Booqr.Tests](Klinkby.Booqr.Tests/AGENTS.md)** - Architecture policy enforcement with ArchUnitNET
- **[Klinkby.Booqr.Core.Tests](Klinkby.Booqr.Core.Tests/AGENTS.md)** - Domain model unit tests
- **[Klinkby.Booqr.Application.Tests](Klinkby.Booqr.Application.Tests/Commands/AGENTS.md)** - Business logic unit tests with mocks
- **[Klinkby.Booqr.Infrastructure.Tests](Klinkby.Booqr.Infrastructure.Tests/Repositories/AGENTS.md)** - Database integration tests
- **[Klinkby.Booqr.Api.Tests](Klinkby.Booqr.Api.Tests/AGENTS.md)** - HTTP endpoint integration tests

Each project has detailed, project-specific guidelines in its `AGENTS.md` file.

## Running Tests

```bash
# Run all tests in solution
dotnet test

# Run tests for specific project
dotnet test tests/Klinkby.Booqr.Application.Tests

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test by name
dotnet test --filter "FullyQualifiedName~TestName"

# Run by category
dotnet test --filter Category=Architecture
```

## Test Independence

- Each test should be independent and isolated
- Tests should not depend on execution order
- Clean up state between tests (use transactions for database tests)
- Use `IClassFixture<T>` for shared expensive setup (database, WebApplicationFactory)

## Coverage Goals

Focus on:
- ✅ Business-critical paths
- ✅ Authorization and security boundaries
- ✅ Error handling and edge cases
- ✅ Architectural policies (via ArchUnit)
- ❌ Don't test framework code or trivial getters/setters
- ❌ Don't aim for 100% coverage - aim for meaningful coverage

## Related Documentation

- **ARCHITECTURE.md** (root) - Architectural policies enforced by tests
- **Individual test project AGENTS.md files** - Project-specific testing practices
- **src/*/AGENTS.md** - Implementation guidelines for each layer
