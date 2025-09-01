namespace Klinkby.Booqr.Application.Locations;

public sealed class GetLocationByIdCommand(
    ILocationRepository locations)
    : ICommand<ByIdRequest, Task<Location?>>
{
    public Task<Location?> Execute(ByIdRequest query, CancellationToken cancellation = default)
    {
        return locations.GetById(query.Id, cancellation);
    }
}
