using Klinkby.Booqr.Application.Users;

namespace Klinkby.Booqr.Application.Tests;

public class GetUserByIdCommandTests
{
    private readonly Mock<IUserRepository> _users = new();

    [Fact]
    public async Task GIVEN_UserExists_WHEN_Execute_THEN_ReturnsUser_And_CallsRepository()
    {
        // Arrange
        var user = new User("someone@example.com", "hash", UserRole.Customer, "Some One", 12345678) { Id = 123 };
        _users.Setup(x => x.GetById(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = new GetUserByIdCommand(_users.Object);

        // Act
        var result = await sut.Execute(new ByIdRequest(user.Id));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user, result);
        _users.Verify(x => x.GetById(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_UserNotFound_WHEN_Execute_THEN_ReturnsNull()
    {
        // Arrange
        int id = 9999;
        _users.Setup(x => x.GetById(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = new GetUserByIdCommand(_users.Object);

        // Act
        var result = await sut.Execute(new ByIdRequest(id));

        // Assert
        Assert.Null(result);
        _users.Verify(x => x.GetById(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
