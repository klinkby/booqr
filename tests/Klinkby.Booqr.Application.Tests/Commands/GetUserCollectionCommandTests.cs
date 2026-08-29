namespace Klinkby.Booqr.Application.Tests.Commands;

public class GetUserCollectionCommandTests
{
    private readonly Mock<IUserRepository> _users = new();

    private GetUserCollectionCommand CreateSut() =>
        new(_users.Object, NullLogger<GetUserCollectionCommand>.Instance);

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Employee_WHEN_Execute_THEN_CallsRepositoryAndReturnsItems(User u1, User u2, User u3)
    {
        // Arrange
        var request = new GetUserCollectionRequest
        {
            Start = 5, Num = 10, K = "k", Role = UserRole.Customer,
            User = CreateUser(1, UserRole.Employee)
        };
        User[] expected =
        [
            u1 with { Role = UserRole.Customer, Id = 1 },
            u2 with { Role = UserRole.Employee, Id = 2 },
            u3 with { Role = UserRole.Admin, Id = 3 }
        ];

        _users.Setup(x => x.Find(request.K, request.Role, request, It.IsAny<CancellationToken>()))
            .Returns(Yield<User>(expected))
            .Verifiable();

        // Act
        var result = await CreateSut().Execute(request);

        // Assert
        var success = Assert.IsType<Result<List<User>>.Success>(result);
        Assert.Equal(expected, success.Value);
        _users.VerifyAll();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Customer_WHEN_ListsEmployees_THEN_CallsRepository(User u1)
    {
        // Arrange
        var request = new GetUserCollectionRequest
        {
            Role = UserRole.Employee,
            User = CreateUser(2) // no roles => Customer
        };
        User[] expected = [u1 with { Role = UserRole.Employee, Id = 3 }];

        _users.Setup(x => x.Find(null, UserRole.Employee, request, It.IsAny<CancellationToken>()))
            .Returns(Yield<User>(expected))
            .Verifiable();

        // Act
        var result = await CreateSut().Execute(request);

        // Assert
        var success = Assert.IsType<Result<List<User>>.Success>(result);
        Assert.Equal(expected, success.Value);
        _users.VerifyAll();
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Admin)]
    [InlineData(null)]
    public async Task GIVEN_Customer_WHEN_ListsNonEmployees_THEN_ForbiddenAndRepositoryNotCalled(string? role)
    {
        // Arrange
        var request = new GetUserCollectionRequest
        {
            Role = role,
            User = CreateUser(2) // no roles => Customer
        };

        // Act
        var result = await CreateSut().Execute(request);

        // Assert
        var fault = Assert.IsType<Result<List<User>>.Fault>(result);
        Assert.Equal(Problem.Forbidden.HttpStatusCode, fault.Problem.HttpStatusCode);
        _users.Verify(
            x => x.Find(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().Execute(null!));
    }
}
