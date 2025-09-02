namespace Klinkby.Booqr.Application.Locations;

public sealed record UpdateLocationRequest(
    [property: Range(1, int.MaxValue)] int Id,
    string Name) : AddLocationRequest(Name), IId;

public sealed class UpdateLocationCommand(
    ILocationRepository locations,
    ILogger<UpdateLocationCommand> logger)
    : UpdateCommand<UpdateLocationRequest, Location>(locations, logger)
{
    protected override Location Map(UpdateLocationRequest query) =>
        new(query.Name, null, null, null, null) { Id = query.Id };
}
