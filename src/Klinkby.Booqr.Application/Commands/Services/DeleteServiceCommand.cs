namespace Klinkby.Booqr.Application.Commands.Services;

public sealed class DeleteServiceCommand(
    IServiceRepository services,
    IActivityRecorder activityRecorder,
    ILogger<DeleteServiceCommand> logger)
    : DeleteCommand<Service>(services, activityRecorder, logger);
