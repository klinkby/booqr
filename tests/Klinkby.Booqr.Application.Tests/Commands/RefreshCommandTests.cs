using RefreshCommand = Klinkby.Booqr.Application.Commands.Auth.RefreshCommand;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class RefreshCommandTests
{
    private readonly RefreshCommand _command;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IOAuth> _oauthMock;
    private readonly Mock<ITransaction> _transactionMock;

    public RefreshCommandTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _oauthMock = new Mock<IOAuth>();
        _transactionMock = new Mock<ITransaction>();

        _command = new RefreshCommand(
            _userRepositoryMock.Object,
            _oauthMock.Object,
            _transactionMock.Object);
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
    public async Task GIVEN_EmptyRefreshToken_WHEN_Execute_THEN_ReturnsNull(string? refreshToken)
    {
        var request = new RefreshRequest(refreshToken!);

        var result = await _command.Execute(request);

        Assert.Null(result);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_InvalidRefreshToken_WHEN_Execute_THEN_ReturnsNull(
        string refreshToken)
    {
        var request = new RefreshRequest(refreshToken);
        _oauthMock.Setup(x => x.GetUserIdFromValidRefreshToken(request.RefreshToken!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await _command.Execute(request);

        Assert.Null(result);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UserNotFound_WHEN_Execute_THEN_ReturnsNull(
        string refreshToken,
        int userId)
    {
        var request = new RefreshRequest(refreshToken);
        _oauthMock.Setup(x => x.GetUserIdFromValidRefreshToken(request.RefreshToken!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _userRepositoryMock.Setup(x => x.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _command.Execute(request);

        Assert.Null(result);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidToken_WHEN_Execute_THEN_ReturnsNewTokenResponse(
        string refreshToken,
        int userId,
        User user,
        OAuthTokenResponse expectedResponse)
    {
        var request = new RefreshRequest(refreshToken);
        _oauthMock.Setup(x => x.GetUserIdFromValidRefreshToken(request.RefreshToken!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _userRepositoryMock.Setup(x => x.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _oauthMock.Setup(x => x.GenerateTokenResponse(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _command.Execute(request);

        Assert.Same(expectedResponse, result);
        _transactionMock.Verify(x => x.Begin(It.IsAny<CancellationToken>()), Times.Once);
        _oauthMock.Verify(x => x.InvalidateToken(request.RefreshToken!, It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ExceptionDuringExecution_WHEN_Execute_THEN_RollsBackTransaction(
        string refreshToken,
        int userId,
        User user,
        Exception testException)
    {
        var request = new RefreshRequest(refreshToken);
        _oauthMock.Setup(x => x.GetUserIdFromValidRefreshToken(request.RefreshToken!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _userRepositoryMock.Setup(x => x.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _oauthMock.Setup(x => x.GenerateTokenResponse(user, It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        await Assert.ThrowsAsync<Exception>(() => _command.Execute(request));

        _transactionMock.Verify(x => x.Begin(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }
}
