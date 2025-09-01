namespace Klinkby.Booqr.Application.Locations;

public sealed class GetLocationsCommand(
    ILocationRepository locations)
    : ICommand<PageQuery, IAsyncEnumerable<Location>>
{
    public IAsyncEnumerable<Location> Execute(PageQuery query, CancellationToken cancellation = default)
    {
        return locations.GetAll(query, cancellation);
    }
}
