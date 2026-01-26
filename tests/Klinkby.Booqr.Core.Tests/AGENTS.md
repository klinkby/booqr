# Core Layer Tests (`Klinkby.Booqr.Core.Tests`)

## Purpose

Unit tests for the Core layer, validating domain models, immutability constraints, and interface contracts. Since Core contains no business logic or I/O, tests focus on data structure behavior and invariants.

All standard testing practices from [tests/AGENTS.md](../AGENTS.md) apply.

## Test Scope

The Core layer contains:
- **Immutable records**: Domain entities (User, Booking, Service, etc.)
- **Interfaces**: Repository and service contracts
- **Exceptions**: Domain-specific exceptions
- **Static classes**: Constants

### What to Test

1. **Record equality**: Verify `with` expressions and structural equality
2. **Validation**: Any domain invariants built into records
3. **Serialization**: If records need to be JSON serializable
4. **Exception behavior**: Custom exception constructors and properties

### What NOT to Test

- ❌ **No business logic** (belongs in Application tests)
- ❌ **No I/O** (belongs in Infrastructure tests)
- ❌ **No integration tests** (no external dependencies)

## Test Patterns

### Testing Immutability

```csharp
[Fact]
public void GIVEN_User_WHEN_UsingWithExpression_THEN_ReturnsNewInstance()
{
    // Arrange
    var original = new User(
        Id: 1,
        Email: "test@example.com",
        Role: UserRole.Customer,
        Created: DateTime.UtcNow,
        Modified: DateTime.UtcNow,
        Version: 1,
        Deleted: null);

    // Act
    var modified = original with { Email = "new@example.com" };

    // Assert
    Assert.NotSame(original, modified);
    Assert.Equal("test@example.com", original.Email);
    Assert.Equal("new@example.com", modified.Email);
}
```

### Testing Record Equality

```csharp
[Theory]
[AutoData]
public void GIVEN_TwoUsersWithSameValues_WHEN_Comparing_THEN_AreEqual(
    int id,
    string email,
    DateTime created)
{
    // Arrange
    var user1 = new User(id, email, UserRole.Customer, created, created, 1, null);
    var user2 = new User(id, email, UserRole.Customer, created, created, 1, null);

    // Act & Assert
    Assert.Equal(user1, user2);
    Assert.True(user1 == user2);
}
```

### Testing Domain Exceptions

```csharp
[Fact]
public void GIVEN_InvalidData_WHEN_ConstructingException_THEN_ContainsExpectedMessage()
{
    // Arrange
    var expectedMessage = "Invalid booking time range";

    // Act
    var exception = new InvalidBookingException(expectedMessage);

    // Assert
    Assert.Equal(expectedMessage, exception.Message);
}
```

## Focus

- Test the public API and contracts, not implementation details
- Group related tests in the same file
- Use nested classes for logical grouping if needed

## Coverage Goals

Since Core contains simple data structures:
- Focus on testing important invariants
- Don't test framework-provided functionality (record equality is built-in)
- Test custom validation logic if present
- Test edge cases for domain constraints

## Related Documentation

- **[tests/AGENTS.md](../AGENTS.md)** - General testing guidelines
- **[src/Klinkby.Booqr.Core/AGENTS.md](../../src/Klinkby.Booqr.Core/AGENTS.md)** - Core layer architectural rules
