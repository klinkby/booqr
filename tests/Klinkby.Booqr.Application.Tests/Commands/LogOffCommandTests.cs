using LogOffCommand = Klinkby.Booqr.Application.Commands.Auth.LogOffCommand;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class LogOffCommandTests
{
    private readonly LogOffCommand _command;
    private readonly Mock<IOAuth> _oauthMock;

    public LogOffCommandTests()
    {
        _oauthMock = new Mock<IOAuth>();
        _command = new LogOffCommand(_oauthMock.Object);
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
    public async Task GIVEN_EmptyRefreshToken_WHEN_Execute_THEN_DoesNotInvalidateToken(string? refreshToken)
    {
        var request = new LogOffRequest(refreshToken!);

        await _command.Execute(request);

        _oauthMock.Verify(x => x.InvalidateToken(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidRefreshToken_WHEN_Execute_THEN_InvalidatesTokenWithNullReplacedBy(
        string refreshToken)
    {
        var request = new LogOffRequest(refreshToken);
        _oauthMock.Setup(x => x.InvalidateToken(refreshToken, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _command.Execute(request);

        _oauthMock.Verify(x => x.InvalidateToken(refreshToken, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_InvalidRefreshToken_WHEN_Execute_THEN_CallsInvalidateToken(
        string invalidRefreshToken)
    {
        var request = new LogOffRequest(invalidRefreshToken);
        _oauthMock.Setup(x => x.InvalidateToken(invalidRefreshToken, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _command.Execute(request);

        _oauthMock.Verify(x => x.InvalidateToken(invalidRefreshToken, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
