namespace Klinkby.Booqr.Application.Commands.Employees;

public sealed class GetEmployeeCollectionCommand(
    IUserRepository users)
    : ICommand<PageQuery, Task<List<Employee>>>
{
    public Task<List<Employee>> Execute(
        PageQuery query,
        CancellationToken cancellation = default)
    {
        return users
            .Find(null, UserRole.Employee, query, cancellation)
            .Select(Map)
            .ToListAsync(cancellation)
            .AsTask();
    }

    private static Employee Map(User user) => new(user);
};
