using System.Globalization;
using Klinkby.Booqr.Application.Util;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class ChangePasswordCommandTests
{
    private readonly static TimeProvider TimeProvider = TestHelpers.TimeProvider;
    private readonly static Mock<IActivityRecorder> ActivityRecorder = new();
    private readonly static ExpiringQueryString ExpiringQueryString = CreateExpiringQueryString(TimeProvider);

    private static ChangePasswordCommand CreateSut(IUserRepository users)
        => new(users, ExpiringQueryString, ActivityRecorder.Object, NullLogger<ChangePasswordCommand>.Instance);

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
        var request = new ChangePasswordRequest("old-pass", "NewPassw0rd!");

        // Act
        bool result = await sut.Execute(request);

        // Assert
        Assert.False(result);
        users.Verify(x => x.GetById(It.IsAny<int>(), CancellationToken.None), Times.Never);
        users.Verify(x => x.Update(It.IsAny<User>(), CancellationToken.None), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CorrectQueryString_WHEN_Execute_THEN_UpdatesAndReturnsTrue(
        User user,
        string newPassword)
    {
        // Arrange
        User? updatedUser = null;
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetById(It.IsAny<int>(), CancellationToken.None)).ReturnsAsync(user);
        users.Setup(x => x.Update(It.IsAny<User>(), CancellationToken.None))
            .Callback<User, CancellationToken>((u, _) => updatedUser = u)
            .ReturnsAsync(true);

        var queryString = ExpiringQueryString.Create(TimeSpan.FromHours(1), new ()
        {
            { Query.Id, user.Id.ToString(CultureInfo.InvariantCulture) },
            { Query.Action, Query.ChangePasswordAction },
            { Query.ETag, user.ETag }
        });

        var sut = CreateSut(users.Object);
        // Intentionally add whitespace to ensure trimming occurs
        var request = new ChangePasswordRequest($"  {newPassword}  ", queryString);

        // Act
        bool result = await sut.Execute(request);

        // Assert
        Assert.True(result);
        users.Verify(x => x.GetById(It.IsAny<int>(), CancellationToken.None), Times.Once);
        users.Verify(x => x.Update(It.IsAny<User>(), CancellationToken.None), Times.Once);
        Assert.NotNull(updatedUser);
        // Email must remain unchanged
        Assert.Equal(user.Email, updatedUser!.Email);
        // Password should be re-hashed and match new password
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(newPassword, updatedUser.PasswordHash));
    }
}
