namespace Klinkby.Booqr.Application.Commands.Locations;

public sealed class DeleteLocationCommand(
    ILocationRepository locations,
    ILogger<DeleteLocationCommand> logger)
    : Abstractions.DeleteCommand<Location>(locations, logger);
