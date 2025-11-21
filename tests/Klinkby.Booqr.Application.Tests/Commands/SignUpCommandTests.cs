using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class SignUpCommandTests
{
    private readonly static TimeProvider TimeProvider = TestHelpers.TimeProvider;
    private readonly static Mock<IActivityRecorder> ActivityRecorder = new();

    private readonly Mock<IUserRepository> _users = new();
    private readonly Channel<Message> _channel = Channel.CreateBounded<Message>(100);

    private SignUpCommand CreateSut() =>
        new(
            _users.Object,
            CreateExpiringQueryString(TimeProvider),
            _channel.Writer,
            ActivityRecorder.Object,
            Options.Create(new PasswordSettings { HmacKey = "" }),
            NullLogger<SignUpCommand>.Instance);

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        SignUpCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Execute(null!));
    }

    [Theory]
    [InlineAutoData("  user@example.com  ")]
    [InlineAutoData("USER@EXAMPLE.COM")]
    public async Task GIVEN_ValidRequest_WHEN_Execute_THEN_MapsAndCallsRepository(string email, User user)
    {
        // Arrange
        const int newUserId = 987;
        var expectedEmail = email.Trim();

        User? capturedUser = null;
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync(newUserId);
        _users.Setup(x => x.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user with { Id = newUserId, Email = expectedEmail });

        var request = new SignUpRequest(email, "https://localhost");

        SignUpCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.Equal(newUserId, result);
        Assert.NotNull(capturedUser);
        Assert.Equal(expectedEmail, capturedUser!.Email);
        Assert.Equal(UserRole.Customer, capturedUser.Role);

        bool hasMessage = _channel.Reader.TryRead(out Message? message);
        Assert.True(hasMessage && message is not null);

        Assert.Equal(expectedEmail, message.To);
        Assert.Contains(expectedEmail,  message.Body, StringComparison.InvariantCulture);
    }
}
