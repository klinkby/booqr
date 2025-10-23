using System.Runtime.Serialization;

namespace Klinkby.Booqr.Application.Locations;

public sealed record UpdateLocationRequest(
    [property: IgnoreDataMember] int Id,
    string Name) : AddLocationRequest(Name), IId;

public sealed class UpdateLocationCommand(
    ILocationRepository locations,
    IETagProvider etagProvider,
    ILogger<UpdateLocationCommand> logger)
    : Abstractions.UpdateCommand<UpdateLocationRequest, Location>(locations, logger)
{
    protected override Location Map(UpdateLocationRequest query) =>
        new(query.Name, null, null, null, null)
        {
            Id = query.Id,
            Version = etagProvider.Version
        };
}
