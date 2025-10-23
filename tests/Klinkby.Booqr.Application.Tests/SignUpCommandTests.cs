using System.Threading.Channels;

namespace Klinkby.Booqr.Application.Tests;

public class SignUpCommandTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Channel<Message> _channel = Channel.CreateBounded<Message>(100);

    private SignUpCommand CreateSut()
    {
        return new SignUpCommand(
            _users.Object,
            _channel.Writer,
            NullLogger<SignUpCommand>.Instance);
    }

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        SignUpCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Execute(null!));
    }

    [Theory]
    [InlineData("  Jane Doe  ", 12345678, "  user@example.com  ")]
    [InlineData("John", 87654321, "USER@EXAMPLE.COM")]
    public async Task GIVEN_ValidRequest_WHEN_Execute_THEN_MapsAndCallsRepository(string name, long phone, string email)
    {
        // Arrange
        const int newUserId = 987;
        User? capturedUser = null;
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync(newUserId);

        var request = new SignUpRequest(name, email, phone);
        var expectedEmail = email.Trim();
        var expectedName = name.Trim();

        SignUpCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.Equal(newUserId, result);
        Assert.NotNull(capturedUser);
        Assert.Equal(expectedEmail, capturedUser!.Email);
        Assert.Equal(expectedName, capturedUser.Name);
        Assert.Equal(phone, capturedUser.Phone);
        Assert.Equal(UserRole.Customer, capturedUser.Role);

        bool hasMessage = _channel.Reader.TryRead(out Message? message);
        Assert.True(hasMessage && message is not null);

        Assert.Equal(expectedEmail, message.To);
        Assert.Contains("password",  message.Body, StringComparison.InvariantCulture);
    }
}
