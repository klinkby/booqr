namespace Klinkby.Booqr.Application.Commands.Locations;

public record AddLocationRequest(
    [property: Required]
    [property: StringLength(0xff)]
    string Name,
    [property: StringLength(0xff)]
    string? Address1,
    [property: StringLength(0xff)]
    string? Address2,
    [property: StringLength(20)]
    string? Zip,
    [property: StringLength(0xff)]
    string? City
    ) : AuthenticatedRequest;

public sealed class AddLocationCommand(
    ILocationRepository locations,
    IActivityRecorder activityRecorder,
    ILogger<AddLocationCommand> logger)
    : AddCommand<AddLocationRequest, Location>(locations, activityRecorder, logger)
{
    protected override Location Map(AddLocationRequest query) =>
        new(query.Name.Trim(),
            query.Address1?.Trim(),
            query.Address2?.Trim(),
            query.Zip?.Trim(),
            query.City?.Trim());
}
