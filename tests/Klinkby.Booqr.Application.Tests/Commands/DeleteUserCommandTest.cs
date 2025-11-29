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
        await sut.Execute(req, CancellationToken.None);

        // Assert
        _users.Verify(x => x.Delete(req.Id, It.IsAny<CancellationToken>()),
            expected ? Times.Once : Times.Never);
        _activityRecorder.Verify(x => x.Delete<User>(
            It.Is<User>(u => u.Id == req.Id), It.IsAny<CancellationToken>()),
            expected ? Times.Once : Times.Never);
}
