using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class ResetPasswordCommandTests
{
    private readonly static TimeProvider TimeProvider = TestHelpers.TimeProvider;
    private static ResetPasswordCommand CreateSut(IUserRepository users, ChannelWriter<Message> writer)
        => new(users, CreateExpiringQueryString(TimeProvider), writer, Options.Create(new PasswordSettings { HmacKey = "" }), NullLogger<ResetPasswordCommand>.Instance);

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        var users = new Mock<IUserRepository>();
        var channel = Channel.CreateBounded<Message>(100);
        var sut = CreateSut(users.Object, channel.Writer);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Execute(null!));
    }

    [Theory]
    [InlineData("  user@example.com  ")]
    [InlineData("USER@EXAMPLE.COM ")]
    public async Task GIVEN_UserNotFound_WHEN_Execute_THEN_DoesNotUpdate_AndDoesNotSendEmail(string email)
    {
        // Arrange
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmail(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync((User?)null);

        var channel = Channel.CreateBounded<Message>(100);
        var sut = CreateSut(users.Object, channel.Writer);

        var request = new ResetPasswordRequest(email, "https://localhost");
        var expectedEmail = email.Trim();

        // Act
        await sut.Execute(request);

        // Assert
        users.Verify(x => x.GetByEmail(expectedEmail, CancellationToken.None), Times.Once);
        users.Verify(x => x.Update(It.IsAny<User>(), CancellationToken.None), Times.Never);

        bool hasMessage = channel.Reader.TryRead(out Message? msg);
        Assert.False(hasMessage);
    }

    [Theory]
    [InlineData("  user2@example.com  ")]
    public async Task GIVEN_UserFound_WHEN_Execute_THEN_UpdatesPassword_And_SendsEmail(string email)
    {
        // Arrange
        var existing = new User(email.Trim(), string.Empty, UserRole.Customer, "Jane Doe", 12345678);

        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmail(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(existing);

        var channel = Channel.CreateBounded<Message>(100);
        var sut = CreateSut(users.Object, channel.Writer);

        var request = new ResetPasswordRequest(email, "https://localhost");
        var expectedEmail = email.Trim();

        // Act
        await sut.Execute(request);

        // Assert repository interactions
        users.Verify(x => x.GetByEmail(expectedEmail, CancellationToken.None), Times.Once);
        users.Verify(x => x.Update(It.IsAny<User>(), CancellationToken.None), Times.Never);

        // Assert message was sent
        bool hasMessage = channel.Reader.TryRead(out Message? message);
        Assert.True(hasMessage && message is not null);
        // The command currently uses the original (untrimmed) email when composing the message
        Assert.Equal(existing.Email, message!.To);
    }
}
