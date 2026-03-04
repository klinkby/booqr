using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.EmployeeServices;

public sealed record GetEmployeeServicesRequest(
    [property: Range(1, int.MaxValue)] int Id,
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100)
    : AuthenticatedRequest, IPageQuery;

public sealed partial class GetEmployeeServicesCommand(
    IEmployeeServiceRepository employeeServices,
    ILogger<GetEmployeeServicesCommand> logger)
    : ICommand<GetEmployeeServicesRequest, IAsyncEnumerable<Service>>
{
    private readonly LoggerMessages _log = new(logger);

    public IAsyncEnumerable<Service> Execute(GetEmployeeServicesRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateAccess(query);

        return employeeServices.GetByEmployeeId(query.Id, query, cancellation);
    }

    private void ValidateAccess(GetEmployeeServicesRequest query)
    {
        if (query.IsOwnerOrEmployee(query.Id)) return;

        _log.CannotInspectEmployeeServices(query.AuthenticatedUserId, query.Id);
        throw new UnauthorizedAccessException("Cannot list another user's employee services");
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(200, LogLevel.Warning,
            "User {UserId} is not permitted to inspect {EmployeeId}'s services")]
        public partial void CannotInspectEmployeeServices(int userId, int employeeId);
    }
}
