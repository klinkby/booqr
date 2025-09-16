using Klinkby.Booqr.Application.Users;
using static Klinkby.Booqr.Application.Tests.TestHelpers;

namespace Klinkby.Booqr.Application.Tests;

public class GetUserCollectionCommandTests
{
    private readonly Mock<IUserRepository> _users = new();

    [Fact]
    public async Task GIVEN_PageQuery_WHEN_Execute_THEN_CallsRepositoryAndReturnsItems()
    {
        // Arrange
        var page = new PageQuery { Start = 10, Num = 5 };
        var expected = new[]
        {
            new User("a@example.com", "h1", UserRole.Customer, "A", 11111111) { Id = 1 },
            new User("b@example.com", "h2", UserRole.Employee, "B", 22222222) { Id = 2 },
            new User("c@example.com", "h3", UserRole.Admin, "C", 33333333) { Id = 3 },
        };

        _users.Setup(x => x.GetAll(page, It.IsAny<CancellationToken>()))
            .Returns(Yield(expected));

        var sut = new GetUserCollectionCommand(_users.Object);

        // Act
        var result = sut.Execute(page);
        var list = await result.ToListAsync();

        // Assert
        Assert.Equal(expected.Length, list.Count);
        Assert.Equal(expected, list);
        _users.Verify(x => x.GetAll(page, It.IsAny<CancellationToken>()), Times.Once);
    }
}
