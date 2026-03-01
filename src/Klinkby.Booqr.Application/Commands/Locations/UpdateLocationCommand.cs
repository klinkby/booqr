using System.Runtime.Serialization;

namespace Klinkby.Booqr.Application.Commands.Locations;

public sealed record UpdateLocationRequest(
    [property: IgnoreDataMember] int Id,
    string Name,
    string? Address1,
    string? Address2,
    string? Zip,
    string? City) : AddLocationRequest(
        Name,
        Address1,
        Address2,
        Zip,
        City), IId;

public sealed class UpdateLocationCommand(
    ILocationRepository locations,
    IRequestMetadata etagProvider,
    IActivityRecorder activityRecorder,
    ILogger<UpdateLocationCommand> logger)
    : UpdateCommand<UpdateLocationRequest, Location>(locations, activityRecorder, logger)
{
    protected override Location Map(UpdateLocationRequest query) =>
        new(query.Name, null, null, null, null)
        {
            Id = query.Id,
            Version = etagProvider.Version
        };
}
