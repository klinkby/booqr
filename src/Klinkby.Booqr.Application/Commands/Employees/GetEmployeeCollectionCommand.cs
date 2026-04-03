namespace Klinkby.Booqr.Application.Commands.Employees;

public sealed record GetEmployeesCollectionRequest;

public sealed class GetEmployeeCollectionCommand(
    IUserRepository users)
    : ICommand<GetEmployeesCollectionRequest, IAsyncEnumerable<Employee>>
{
    public IAsyncEnumerable<Employee> Execute(
        GetEmployeesCollectionRequest _,
        CancellationToken cancellation = default)
    {
        return users.Find(
            null,
            UserRole.Employee,
            new PageQuery(),
            cancellation).Select(Map);
    }

    private static Employee Map(User user) => new(user);
};
