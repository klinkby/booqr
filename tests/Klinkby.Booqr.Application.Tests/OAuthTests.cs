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

        var actual = await sut.GenerateTokenResponse(user, TestContext.Current.CancellationToken);
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

        var actual = await sut.GenerateTokenResponse(user, TestContext.Current.CancellationToken);

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

        var actual = await sut.GenerateTokenResponse(user, TestContext.Current.CancellationToken);

        Assert.NotNull(actual.RefreshToken);
        Assert.Equal(40, actual.RefreshToken?.Length);
        repoMock.VerifyAll();
    }

    private static Mock<IRefreshTokenRepository> CreateRepositoryMock()
    {
        var repo = new Mock<IRefreshTokenRepository>();
        repo.Setup(x => x.Add(It.Is<RefreshToken>(m => m.Hash.Length == 40), It.IsAny<CancellationToken>()));
        return repo;
    }
}
