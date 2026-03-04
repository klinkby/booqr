using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.EmployeeServices;

public sealed record DeleteEmployeeServiceRequest(
    [property: Range(1, int.MaxValue)] int EmployeeId,
    [property: Range(1, int.MaxValue)] int ServiceId)
    : AuthenticatedRequest;

public sealed partial class DeleteEmployeeServiceCommand(
    IEmployeeServiceRepository employeeServices,
    IActivityRecorder activityRecorder,
    ILogger<DeleteEmployeeServiceCommand> logger)
    : ICommand<DeleteEmployeeServiceRequest>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task Execute(DeleteEmployeeServiceRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        _log.UnassignService(query.AuthenticatedUserId, query.EmployeeId, query.ServiceId);
        var deleted = await employeeServices.Delete(query.EmployeeId, query.ServiceId, cancellation);
        if (deleted) activityRecorder.Delete<EmployeeService>(new(query.AuthenticatedUserId, EmployeeService.CompositeId(query.EmployeeId, query.ServiceId)));
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(202, LogLevel.Information,
            "User {UserId} removed service {ServiceId} from employee {EmployeeId}")]
        public partial void UnassignService(int userId, int employeeId, int serviceId);
    }
}
