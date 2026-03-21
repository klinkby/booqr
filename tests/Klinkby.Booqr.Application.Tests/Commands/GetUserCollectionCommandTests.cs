namespace Klinkby.Booqr.Application.Tests.Commands;

public class GetUserCollectionCommandTests
{
    private readonly Mock<IUserRepository> _users = new();

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_GetUserCollectionRequest_WHEN_Execute_THEN_CallsRepositoryAndReturnsItems(User u1, User u2, User u3)
    {
        // Arrange
        var request = new GetUserCollectionRequest { Start = 5, Num = 10, K = "k", Role = "Customer" };
        User[] expected = new[]
        {
            u1 with { Role = UserRole.Customer, Id = 1 },
            u2 with { Role = UserRole.Employee, Id = 2 },
            u3 with { Role = UserRole.Admin, Id = 3 }
        };

        _users.Setup(x => x.Find(request.K, request.Role, request, It.IsAny<CancellationToken>()))
            .Returns(Yield(expected))
            .Verifiable();

        var sut = new GetUserCollectionCommand(_users.Object);

        // Act
        IAsyncEnumerable<User> result = sut.Execute(request);
        List<User> list = await result.ToListAsync();

        // Assert
        Assert.Equal(expected.Length, list.Count);
        Assert.Equal(expected, list);
        _users.VerifyAll();
    }
}
