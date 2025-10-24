namespace Klinkby.Booqr.Application.Commands.Locations;

public sealed class GetLocationCollectionCommand(
    ILocationRepository locations)
    : GetCollectionCommand<PageQuery, Location>(locations);
