namespace Klinkby.Booqr.Application.Tests;

public class ChangePasswordCommandTests
{
    private readonly static Mock<IActivityRecorder> _activityRecorder = new();

    private static ChangePasswordCommand CreateSut(IUserRepository users)
        => new(users, _activityRecorder.Object, NullLogger<ChangePasswordCommand>.Instance);

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        var users = new Mock<IUserRepository>();
        var sut = CreateSut(users.Object);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Execute(null!));
    }

    [Fact]
    public async Task GIVEN_UserNotFound_WHEN_Execute_THEN_ReturnsFalse_AndDoesNotUpdate()
    {
        // Arrange
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetById(It.IsAny<int>(), CancellationToken.None)).ReturnsAsync((User?)null);

        var sut = CreateSut(users.Object);
        var request = new ChangePasswordRequest("old-pass", "NewPassw0rd!")
        {
            User = ApplicationAutoDataAttribute.GetTestUser()
        };

        // Act
        bool result = await sut.Execute(request);

        // Assert
        Assert.False(result);
        users.Verify(x => x.GetById(It.IsAny<int>(), CancellationToken.None), Times.Once);
        users.Verify(x => x.Update(It.IsAny<User>(), CancellationToken.None), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_WrongOldPassword_WHEN_Execute_THEN_ReturnsFalse_AndDoesNotUpdate(
        User user,
        string correctOldPassword,
        string wrongOldPassword)
    {
        // Arrange
        var existing = user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(correctOldPassword) };
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetById(It.IsAny<int>(), CancellationToken.None)).ReturnsAsync(existing);

        var sut = CreateSut(users.Object);
        var request = new ChangePasswordRequest(wrongOldPassword, "NewPassw0rd!")
        {
            User = ApplicationAutoDataAttribute.GetTestUser()
        };

        // Act
        bool result = await sut.Execute(request);

        // Assert
        Assert.False(result);
        users.Verify(x => x.GetById(It.IsAny<int>(), CancellationToken.None), Times.Once);
        users.Verify(x => x.Update(It.IsAny<User>(), CancellationToken.None), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CorrectOldPassword_WHEN_Execute_THEN_UpdatesAndReturnsTrue(
        User user,
        string oldPassword,
        string newPassword)
    {
        // Arrange
        var existing = user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(oldPassword) };
        User? updatedUser = null;
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetById(It.IsAny<int>(), CancellationToken.None)).ReturnsAsync(existing);
        users.Setup(x => x.Update(It.IsAny<User>(), CancellationToken.None))
            .Callback<User, CancellationToken>((u, _) => updatedUser = u)
            .ReturnsAsync(true);

        var sut = CreateSut(users.Object);
        // Intentionally add whitespace to ensure trimming occurs
        var request = new ChangePasswordRequest($"  {oldPassword}  ", $"  {newPassword}  ")
        {
            User = ApplicationAutoDataAttribute.GetTestUser()
        };

        // Act
        bool result = await sut.Execute(request);

        // Assert
        Assert.True(result);
        users.Verify(x => x.GetById(It.IsAny<int>(), CancellationToken.None), Times.Once);
        users.Verify(x => x.Update(It.IsAny<User>(), CancellationToken.None), Times.Once);
        Assert.NotNull(updatedUser);
        // Email must remain unchanged
        Assert.Equal(existing.Email, updatedUser!.Email);
        // Password should be re-hashed and match new password
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(newPassword, updatedUser.PasswordHash));
    }
}
