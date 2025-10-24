namespace Klinkby.Booqr.Application.Commands.Locations;

public record AddLocationRequest(
    [property: Required]
    [property: StringLength(0xff)]
    string Name) : AuthenticatedRequest;

public sealed class AddLocationCommand(
    ILocationRepository locations,
    ILogger<AddLocationCommand> logger)
    : AddCommand<AddLocationRequest, Location>(locations, logger)
{
    protected override Location Map(AddLocationRequest query) =>
        new(query.Name, null, null, null, null);
}
