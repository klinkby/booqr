namespace Klinkby.Booqr.Application.Commands.EmployeeServices;

public sealed record GetEmployeeServicesRequest(
    [property: Range(1, int.MaxValue)] int Id,
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100)
    : IPageQuery;

public sealed class GetEmployeeServicesCommand(IEmployeeServiceRepository employeeServices)
    : ICommand<GetEmployeeServicesRequest, IAsyncEnumerable<Service>>
{
    public IAsyncEnumerable<Service> Execute(GetEmployeeServicesRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return employeeServices.GetByEmployeeId(query.Id, query, cancellation);
    }
}
