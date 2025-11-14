namespace Klinkby.Booqr.Application.Commands.Locations;

public sealed class DeleteLocationCommand(
    ILocationRepository locations,
    IActivityRecorder activityRecorder,
    ILogger<DeleteLocationCommand> logger)
    : DeleteCommand<Location>(locations, activityRecorder, logger);
