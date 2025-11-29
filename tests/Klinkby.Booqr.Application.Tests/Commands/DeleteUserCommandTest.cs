namespace Klinkby.Booqr.Application.Tests.Commands;

public class DeleteUserCommandTest
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    private DeleteUserCommand CreateSut() =>
         new(_users.Object, _activityRecorder.Object, NullLogger<DeleteUserCommand>.Instance);

    [Theory]
    [InlineAutoData(-1, true)]
    [InlineAutoData(0, false)]
    public async Task GIVEN_OtherUser_WHEN_Execute_THEN_ReturnsTrue(int userIncrement, bool expected, int userId)
    {
        // Arrange
        AuthenticatedByIdRequest req = new(userId) { User = CreateUser(userId + userIncrement) };

        _users.Setup(x => x.Delete(req.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        DeleteUserCommand sut = CreateSut();

        // Act
        var deleted = await sut.Delete(req, CancellationToken.None);

        // Assert
        Assert.Equal(expected, deleted);
        _users.Verify(x => x.Delete(req.Id, It.IsAny<CancellationToken>()),
            expected ? Times.Once : Times.Never);
    }
}
