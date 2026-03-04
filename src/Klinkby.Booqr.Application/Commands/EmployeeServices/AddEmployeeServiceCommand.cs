using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.EmployeeServices;

public sealed record AddEmployeeServiceRequest(
    [property: Range(1, int.MaxValue)] int EmployeeId,
    [property: Range(1, int.MaxValue)] int ServiceId)
    : AuthenticatedRequest;

public sealed partial class AddEmployeeServiceCommand(
    IEmployeeServiceRepository employeeServices,
    IActivityRecorder activityRecorder,
    ILogger<AddEmployeeServiceCommand> logger)
    : ICommand<AddEmployeeServiceRequest>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task Execute(AddEmployeeServiceRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        _log.AssignService(query.AuthenticatedUserId, query.EmployeeId, query.ServiceId);
        await employeeServices.Add(query.EmployeeId, query.ServiceId, cancellation);
        activityRecorder.Add<EmployeeService>(new(query.AuthenticatedUserId, EmployeeService.CompositeId(query.EmployeeId, query.ServiceId)));
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(201, LogLevel.Information,
            "User {UserId} assigned service {ServiceId} to employee {EmployeeId}")]
        public partial void AssignService(int userId, int employeeId, int serviceId);
    }
}
