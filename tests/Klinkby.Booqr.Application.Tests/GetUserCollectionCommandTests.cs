namespace Klinkby.Booqr.Application.Tests;

public class GetUserCollectionCommandTests
{
    private readonly Mock<IUserRepository> _users = new();

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_PageQuery_WHEN_Execute_THEN_CallsRepositoryAndReturnsItems(PageQuery page, User u1, User u2, User u3)
    {
        // Arrange
        page = new PageQuery { Start = 10, Num = 5 };
        User[] expected = new[]
        {
            u1 with { Role = UserRole.Customer, Id = 1 },
            u2 with { Role = UserRole.Employee, Id = 2 },
            u3 with { Role = UserRole.Admin, Id = 3 }
        };

        _users.Setup(x => x.GetAll(page, It.IsAny<CancellationToken>()))
            .Returns(Yield(expected));

        var sut = new GetUserCollectionCommand(_users.Object);

        // Act
        IAsyncEnumerable<User> result = sut.Execute(page);
        List<User> list = await result.ToListAsync();

        // Assert
        Assert.Equal(expected.Length, list.Count);
        Assert.Equal(expected, list);
        _users.Verify(x => x.GetAll(page, It.IsAny<CancellationToken>()), Times.Once);
    }
}
