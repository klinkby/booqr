namespace Klinkby.Booqr.Application.Locations;

public sealed class GetLocationCollectionCommand(
    ILocationRepository locations)
    : GetCollectionCommand<PageQuery, Location>(locations);
