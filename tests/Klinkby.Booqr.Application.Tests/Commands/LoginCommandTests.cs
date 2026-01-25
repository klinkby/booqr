using LoginCommand = Klinkby.Booqr.Application.Commands.Auth.LoginCommand;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class LoginCommandTests
{
    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UserDoesNotExist_WHEN_Login_THEN_ReturnsNull(
        string email,
        string password,
        OAuthTokenResponse expectedResponse)
    {
        Mock<IUserRepository> userRepo = CreateUserRepositoryMock(null);
        Mock<IOAuth> oauth = CreateOAuthMock(expectedResponse);

        var command = new LoginCommand(userRepo.Object, oauth.Object, NullLogger<LoginCommand>.Instance);
        var request = new LoginRequest(email, password);

        OAuthTokenResponse? result = await command.Execute(request);

        Assert.Null(result);
        userRepo.Verify(x => x.GetByEmail(email, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_WrongPassword_WHEN_Login_THEN_ReturnsNull(
        User user,
        string correctPassword,
        string wrongPassword,
        OAuthTokenResponse expectedResponse)
    {
        User userWithHashedPassword =
            user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(correctPassword) };
        Mock<IUserRepository> userRepo = CreateUserRepositoryMock(userWithHashedPassword);
        Mock<IOAuth> oauth = CreateOAuthMock(expectedResponse);
        var command = new LoginCommand(userRepo.Object, oauth.Object,  NullLogger<LoginCommand>.Instance);
        var request = new LoginRequest(user.Email, wrongPassword);

        OAuthTokenResponse? result = await command.Execute(request);

        Assert.Null(result);
        userRepo.Verify(x => x.GetByEmail(user.Email, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CorrectCredentials_WHEN_Login_THEN_ReturnsBearerToken(
        User user,
        string password,
        OAuthTokenResponse expectedResponse, string refreshToken)
    {
        User userWithHashedPassword = user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password) };
        Mock<IUserRepository> userRepo = CreateUserRepositoryMock(userWithHashedPassword);
        Mock<IOAuth> oauth = CreateOAuthMock(expectedResponse);
        var command = new LoginCommand(userRepo.Object, oauth.Object, NullLogger<LoginCommand>.Instance);
        var request = new LoginRequest(userWithHashedPassword.Email, password) { RefreshToken = refreshToken };

        // Act
        OAuthTokenResponse? response = await command.Execute(request);

        // Assert basic response
        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal(expectedResponse.TokenType, response.TokenType);

        // Verify interactions
        userRepo.Verify(x => x.GetByEmail(user.Email, CancellationToken.None), Times.Once);
        oauth.Verify(x => x.RevokeTokenFamily(refreshToken, CancellationToken.None), Times.Once);
    }

    private static Mock<IUserRepository> CreateUserRepositoryMock(User? user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(x => x
                .GetByEmail(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(user);
        return repo;
    }

    private static Mock<IOAuth> CreateOAuthMock(OAuthTokenResponse fakeResponse)
    {
        var mock = new Mock<IOAuth>();
        mock.Setup(m => m.GenerateTokenResponse(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult((fakeResponse, "tokenHash")));
        return mock;
    }
}
