namespace Klinkby.Booqr.Application.Locations;

public sealed class DeleteLocationCommand(
    ILocationRepository locations,
    ILogger<DeleteLocationCommand> logger)
    : DeleteCommand<Location>(locations, logger);
