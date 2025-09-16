using Klinkby.Booqr.Application.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace Klinkby.Booqr.Application.Tests;

public class SignUpCommandTests
{
    private readonly Mock<IUserRepository> _users = new();

    private SignUpCommand CreateSut() => new(
        _users.Object,
        NullLogger<SignUpCommand>.Instance);

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Execute(null!));
    }

    [Theory]
    [InlineData("  Jane Doe  ", 12345678, "  user@example.com  ", "  P@ssw0rd!  ")]
    [InlineData("John", 87654321, "USER@EXAMPLE.COM", "S0mething#Hard")]
    public async Task GIVEN_ValidRequest_WHEN_Execute_THEN_MapsAndCallsRepository(string name, long phone, string email, string password)
    {
        // Arrange
        const int newUserId = 987;
        User? capturedUser = null;
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync(newUserId);

        var request = new SignUpRequest(name, phone, email, password);
        var expectedEmail = email.Trim();
        var expectedName = name.Trim();
        var expectedPassword = password.Trim();

        var sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.Equal(newUserId, result);
        Assert.NotNull(capturedUser);
        Assert.Equal(expectedEmail, capturedUser!.Email);
        Assert.Equal(expectedName, capturedUser.Name);
        Assert.Equal(phone, capturedUser.Phone);
        Assert.Equal(UserRole.Customer, capturedUser.Role);
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(expectedPassword, capturedUser.PasswordHash));
    }
}
