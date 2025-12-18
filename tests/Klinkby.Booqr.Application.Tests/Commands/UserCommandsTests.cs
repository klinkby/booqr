using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class UserCommandsTests
{
    private readonly static TimeProvider TimeProvider = new FakeTimeProvider();
    private readonly JwtSettings _jwt = new()
    {
        Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        Issuer = "test-issuer",
        Audience = "test-audience"
    };

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UserDoesNotExist_WHEN_Login_THEN_ReturnsNull(
        string email,
        string password)
    {
        Mock<IUserRepository> repo = CreateUserRepositoryMock(null);

        var command = new LoginCommand(repo.Object, Options.Create(_jwt), TimeProvider, NullLogger<LoginCommand>.Instance);
        var request = new LoginRequest(email, password);

        LoginResponse? result = await command.Execute(request);

        Assert.Null(result);
        repo.Verify(x => x.GetByEmail(email, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_WrongPassword_WHEN_Login_THEN_ReturnsNull(
        User user,
        string correctPassword,
        string wrongPassword)
    {
        User userWithHashedPassword =
            user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(correctPassword) };
        Mock<IUserRepository> repo = CreateUserRepositoryMock(userWithHashedPassword);
        var command = new LoginCommand(repo.Object, Options.Create(_jwt), TimeProvider, NullLogger<LoginCommand>.Instance);
        var request = new LoginRequest(user.Email, wrongPassword);

        LoginResponse? result = await command.Execute(request);

        Assert.Null(result);
        repo.Verify(x => x.GetByEmail(user.Email, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CorrectCredentials_WHEN_Login_THEN_ReturnsBearerToken(
        User user,
        string password)
    {
        User userWithHashedPassword = user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password) };
        Mock<IUserRepository> repo = CreateUserRepositoryMock(userWithHashedPassword);

        var command = new LoginCommand(repo.Object, Options.Create(_jwt), TimeProvider, NullLogger<LoginCommand>.Instance);
        var request = new LoginRequest(userWithHashedPassword.Email, password);

        // Act
        LoginResponse? response = await command.Execute(request);

        // Assert basic response
        Assert.NotNull(response);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal((int)TimeSpan.FromHours(8).TotalSeconds, response.ExpiresIn);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));

        // Optional: decode token to assert issuer/audience and claims exist
        var handler = new JwtSecurityTokenHandler();
        // Clear default claim mapping to get JWT standard claim names
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        JwtSecurityToken? jwt = handler.ReadJwtToken(response.AccessToken);
        Assert.Equal(_jwt.Issuer, jwt.Issuer);
        Assert.Contains(_jwt.Audience, jwt.Audiences);
        Assert.Contains(jwt.Claims, c => c.Type == "nameid" && c.Value == $"{userWithHashedPassword.Id}");
        Assert.Contains(jwt.Claims, c => c.Type == "unique_name" && c.Value == userWithHashedPassword.Email);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == userWithHashedPassword.Role);

        repo.Verify(x => x.GetByEmail(user.Email, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CorrectCredentials_WHEN_Login_THEN_ReturnsRefreshToken(
        User user,
        string password)
    {
        User userWithHashedPassword = user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password) };
        Mock<IUserRepository> repo = CreateUserRepositoryMock(userWithHashedPassword);

        var command = new LoginCommand(repo.Object, Options.Create(_jwt), TimeProvider, NullLogger<LoginCommand>.Instance);
        var request = new LoginRequest(userWithHashedPassword.Email, password);

        // Act
        LoginResponse? response = await command.Execute(request);

        // Assert refresh token exists and is valid
        Assert.NotNull(response);
        Assert.NotNull(response.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));

        // Decode and validate refresh token
        var handler = new JwtSecurityTokenHandler();
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        JwtSecurityToken? refreshJwt = handler.ReadJwtToken(response.RefreshToken);
        
        Assert.Equal(_jwt.Issuer, refreshJwt.Issuer);
        Assert.Contains(_jwt.Audience, refreshJwt.Audiences);
        Assert.Contains(refreshJwt.Claims, c => c.Type == "nameid" && c.Value == $"{userWithHashedPassword.Id}");
        Assert.Contains(refreshJwt.Claims, c => c.Type == "token_type" && c.Value == "refresh");
        
        // Verify refresh token has longer expiration (7 days default)
        var expectedExpiration = TimeProvider.GetUtcNow().AddDays(7);
        Assert.True(refreshJwt.ValidTo >= expectedExpiration.UtcDateTime);

        repo.Verify(x => x.GetByEmail(user.Email, CancellationToken.None), Times.Once);
    }

    private static Mock<IUserRepository> CreateUserRepositoryMock(User? user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(x => x
                .GetByEmail(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(user);
        return repo;
    }
}
