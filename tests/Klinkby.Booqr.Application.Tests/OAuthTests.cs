using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Tests;

public class OAuthTests
{
    [Theory]
    [ApplicationAutoData]
    internal async Task GIVEN_User_WHEN_GenerateTokenResponse_THEN_ReturnsToken(User user, JwtSettings settings)
    {
        var repoMock = CreateRepositoryMock();
        var sut = new OAuth(repoMock.Object, TestHelpers.TimeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        (var actual, _) = await sut.GenerateTokenResponse(user, TestContext.Current.CancellationToken);
        Assert.NotNull(actual);
        Assert.NotEmpty(actual.AccessToken);
        Assert.NotEmpty(actual.RefreshToken!);
        Assert.True(actual.ExpiresIn > 0);
        Assert.True(DateTime.UnixEpoch < actual.RefreshTokenExpiration);
    }

    [Theory]
    [ApplicationAutoData]
    internal async Task GIVEN_User_WHEN_GenerateTokenResponse_THEN_AddToRepository(User user, JwtSettings settings)
    {
        var repoMock = CreateRepositoryMock();
        var sut = new OAuth(repoMock.Object, TestHelpers.TimeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        await sut.GenerateTokenResponse(user, TestContext.Current.CancellationToken);

        repoMock.Verify(x => x.Add(It.Is<RefreshToken>(rt => rt.UserId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_User_WHEN_GenerateTokenResponse_THEN_AccessTokenPropertiesAreValid(User user,
        JwtSettings settings)
    {
        var repoMock = CreateRepositoryMock();
        var sut = new OAuth(repoMock.Object, TestHelpers.TimeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        (var actual, _) = await sut.GenerateTokenResponse(user, TestContext.Current.CancellationToken);

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        var jwt = handler.ReadJwtToken(actual.AccessToken);

        Assert.Equal(settings.Issuer, jwt.Issuer);
        Assert.Contains(settings.Audience, jwt.Audiences);
        Assert.Equal(user.Id.ToString(CultureInfo.InvariantCulture), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.Role, jwt.Claims.First(c => c.Type == "role").Value);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_User_WHEN_GenerateTokenResponse_THEN_RefreshTokenPropertiesAreValid(User user,
        JwtSettings settings)
    {
        var repoMock = CreateRepositoryMock();
        var sut = new OAuth(repoMock.Object, TestHelpers.TimeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        (var actual, _) = await sut.GenerateTokenResponse(user, TestContext.Current.CancellationToken);

        Assert.NotNull(actual.RefreshToken);
        Assert.Equal(40, actual.RefreshToken?.Length);
        repoMock.VerifyAll();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_RefreshToken_WHEN_InvalidateToken_THEN_RevokeInRepository(string refreshToken, JwtSettings settings)
    {
        var repoMock = new Mock<IRefreshTokenRepository>();
        var timeProvider = TestHelpers.TimeProvider;
        var sut = new OAuth(repoMock.Object, timeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        await sut.InvalidateToken(refreshToken, null, TestContext.Current.CancellationToken);

        repoMock.Verify(x => x.RevokeSingle(
            It.Is<string>(s => !string.IsNullOrEmpty(s)),
            timeProvider.GetUtcNow().UtcDateTime,
            null,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NonExistentToken_WHEN_GetUserIdFromValidRefreshToken_THEN_ReturnsNull(string refreshToken, JwtSettings settings)
    {
        var repoMock = new Mock<IRefreshTokenRepository>();
        repoMock.Setup(x => x.GetByHash(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        var sut = new OAuth(repoMock.Object, TestHelpers.TimeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        var actual = await sut.GetUserIdFromValidRefreshToken(refreshToken, TestContext.Current.CancellationToken);

        Assert.Null(actual);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_RevokedToken_WHEN_GetUserIdFromValidRefreshToken_THEN_RevokesFamilyAndReturnsNull(string refreshToken, RefreshToken tokenMetadata, JwtSettings settings)
    {
        var repoMock = new Mock<IRefreshTokenRepository>();
        tokenMetadata = tokenMetadata with { Revoked = DateTime.UtcNow.AddMinutes(-1) };
        repoMock.Setup(x => x.GetByHash(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenMetadata);
        var sut = new OAuth(repoMock.Object, TestHelpers.TimeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        var actual = await sut.GetUserIdFromValidRefreshToken(refreshToken, TestContext.Current.CancellationToken);

        Assert.Null(actual);
        repoMock.Verify(x => x.RevokeAll(tokenMetadata.Family, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ExpiredToken_WHEN_GetUserIdFromValidRefreshToken_THEN_ReturnsNull(string refreshToken, RefreshToken tokenMetadata, JwtSettings settings)
    {
        var repoMock = new Mock<IRefreshTokenRepository>();
        var timeProvider = TestHelpers.TimeProvider;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        tokenMetadata = tokenMetadata with { Revoked = null, Expires = now.AddMinutes(-1) };
        repoMock.Setup(x => x.GetByHash(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenMetadata);
        var sut = new OAuth(repoMock.Object, timeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        var actual = await sut.GetUserIdFromValidRefreshToken(refreshToken, TestContext.Current.CancellationToken);

        Assert.Null(actual);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidToken_WHEN_GetUserIdFromValidRefreshToken_THEN_ReturnsUserId(string refreshToken, RefreshToken tokenMetadata, JwtSettings settings)
    {
        var repoMock = new Mock<IRefreshTokenRepository>();
        var timeProvider = TestHelpers.TimeProvider;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        tokenMetadata = tokenMetadata with { Revoked = null, Expires = now.AddMinutes(1) };
        repoMock.Setup(x => x.GetByHash(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenMetadata);
        var sut = new OAuth(repoMock.Object, timeProvider, Options.Create(settings), NullLogger<OAuth>.Instance);

        var actual = await sut.GetUserIdFromValidRefreshToken(refreshToken, TestContext.Current.CancellationToken);

        Assert.Equal(tokenMetadata.UserId, actual);
    }

    private static Mock<IRefreshTokenRepository> CreateRepositoryMock()
    {
        var repo = new Mock<IRefreshTokenRepository>();
        repo.Setup(x => x.Add(It.Is<RefreshToken>(m => m.Hash.Length == 40), It.IsAny<CancellationToken>()));
        return repo;
    }
}
