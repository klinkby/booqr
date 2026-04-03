using Klinkby.Booqr.Application.Commands.Employees;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class GetEmployeeCollectionCommandTests
{

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_GetEmployeesCollectionRequest_WHEN_Execute_THEN_CallsRepositoryWithEmployeeRoleAndReturnsEmployees(GetEmployeesCollectionRequest request, User[] expected)
    {
        // Arrange
        Mock<IUserRepository> userRepository = new();
        userRepository.Setup(x => x.Find(null, UserRole.Employee, It.IsAny<PageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(expected))
            .Verifiable();

        var sut = new GetEmployeeCollectionCommand(userRepository.Object);

        // Act
        List<Employee> actual = await sut
            .Execute(request, Current.CancellationToken)
            .ToListAsync(Current.CancellationToken);

        // Assert
        Assert.Equal(expected.Length, actual.Count);
        Assert.All(expected, (e) => Assert.Contains(actual, a => a.Id == e.Id));

        userRepository.VerifyAll();
    }

}
