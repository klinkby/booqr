namespace Klinkby.Booqr.Application.Tests.Commands;

public class LogoutCommandTests
{
    private readonly LogoutCommand _command;
    private readonly Mock<IOAuth> _oauthMock;

    public LogoutCommandTests()
    {
        _oauthMock = new Mock<IOAuth>();
        _command = new LogoutCommand(_oauthMock.Object);
    }

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _command.Execute(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GIVEN_EmptyRefreshToken_WHEN_Execute_THEN_DoesNotRevokeTokenFamily(string? refreshToken)
    {
        var request = new LogoutRequest { RefreshToken = refreshToken };

        await _command.Execute(request);

        _oauthMock.Verify(x => x.RevokeTokenFamily(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidRefreshToken_WHEN_Execute_THEN_RevokesTokenFamily(
        string refreshToken)
    {
        var request = new LogoutRequest { RefreshToken = refreshToken };
        _oauthMock.Setup(x => x.RevokeTokenFamily(refreshToken, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _command.Execute(request);

        _oauthMock.Verify(x => x.RevokeTokenFamily(refreshToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_InvalidRefreshToken_WHEN_Execute_THEN_CallsRevokeTokenFamily(
        string invalidRefreshToken)
    {
        var request = new LogoutRequest { RefreshToken = invalidRefreshToken };
        _oauthMock.Setup(x => x.RevokeTokenFamily(invalidRefreshToken, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _command.Execute(request);

        _oauthMock.Verify(x => x.RevokeTokenFamily(invalidRefreshToken, It.IsAny<CancellationToken>()), Times.Once);
    }
}
