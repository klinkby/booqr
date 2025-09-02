namespace Klinkby.Booqr.Application.Locations;

public sealed class GetLocationByIdCommand(
    ILocationRepository locations)
    : GetByIdCommand<Location>(locations);
