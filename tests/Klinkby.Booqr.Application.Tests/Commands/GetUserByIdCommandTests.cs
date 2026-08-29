using Klinkby.Booqr.Core.Exceptions;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class GetUserByIdCommandTests
{
    private readonly Mock<IUserRepository> _users = new();

    private GetUserByIdCommand CreateSut() =>
        new(_users.Object, NullLogger<GetUserByIdCommand>.Instance);

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerRequestsOwnRecord_WHEN_Execute_THEN_ReturnsUser(User user)
    {
        // Arrange
        var target = user with { Id = 2, Role = UserRole.Customer };
        _users.Setup(x => x.GetById(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        var request = new GetUserByIdRequest(target.Id) { User = CreateUser(target.Id) };

        // Act
        var result = await CreateSut().Execute(request);

        // Assert
        var success = Assert.IsType<Result<User>.Success>(result);
        Assert.Equal(target, success.Value);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerRequestsEmployeeRecord_WHEN_Execute_THEN_ReturnsUser(User user)
    {
        // Arrange
        var target = user with { Id = 5, Role = UserRole.Employee };
        _users.Setup(x => x.GetById(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        var request = new GetUserByIdRequest(target.Id) { User = CreateUser(2) }; // customer

        // Act
        var result = await CreateSut().Execute(request);

        // Assert
        var success = Assert.IsType<Result<User>.Success>(result);
        Assert.Equal(target, success.Value);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerRequestsOtherCustomerRecord_WHEN_Execute_THEN_ForbiddenAndNoDataLeaked(User user)
    {
        // Arrange
        var target = user with { Id = 9, Role = UserRole.Customer };
        _users.Setup(x => x.GetById(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        var request = new GetUserByIdRequest(target.Id) { User = CreateUser(2) }; // different customer

        // Act
        var result = await CreateSut().Execute(request);

        // Assert: forbidden, and the loaded record is NOT carried out in the result.
        var fault = Assert.IsType<Result<User>.Fault>(result);
        Assert.Equal(Problem.Forbidden.HttpStatusCode, fault.Problem.HttpStatusCode);
        Assert.DoesNotContain(target.Email, fault.Problem.Detail ?? "", StringComparison.Ordinal);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AnonymousRequestsCustomerRecord_WHEN_Execute_THEN_DoesNotReturnData(User user)
    {
        // A request that reached the command with no identity (route policy bypassed) must
        // never return another customer's record. AuthenticatedUserId has no claim to read,
        // so the owner check fails closed by throwing rather than returning the record.
        // Arrange
        var target = user with { Id = 9, Role = UserRole.Customer };
        _users.Setup(x => x.GetById(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        var request = new GetUserByIdRequest(target.Id); // no User set => anonymous

        // Act / Assert: no Success carrying the record is ever produced.
        await Assert.ThrowsAsync<InvalidClaimException>(() => CreateSut().Execute(request));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_EmployeeRequestsAnyCustomerRecord_WHEN_Execute_THEN_ReturnsUser(User user)
    {
        // Arrange
        var target = user with { Id = 9, Role = UserRole.Customer };
        _users.Setup(x => x.GetById(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        var request = new GetUserByIdRequest(target.Id) { User = CreateUser(1, UserRole.Employee) };

        // Act
        var result = await CreateSut().Execute(request);

        // Assert
        var success = Assert.IsType<Result<User>.Success>(result);
        Assert.Equal(target, success.Value);
    }

    [Fact]
    public async Task GIVEN_UserNotFound_WHEN_Execute_THEN_Fault()
    {
        // Arrange
        const int id = 9999;
        _users.Setup(x => x.GetById(id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var request = new GetUserByIdRequest(id) { User = CreateUser(2) };

        // Act
        var result = await CreateSut().Execute(request);

        // Assert
        Assert.IsType<Result<User>.Fault>(result);
        _users.Verify(x => x.GetById(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
