namespace Klinkby.Booqr.Application.Commands.Locations;

public sealed class GetLocationByIdCommand(
    ILocationRepository locations)
    : GetByIdCommand<Location>(locations);
