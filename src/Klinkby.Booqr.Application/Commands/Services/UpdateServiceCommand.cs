using System.Runtime.Serialization;

namespace Klinkby.Booqr.Application.Commands.Services;

public sealed record UpdateServiceRequest(
    [property: IgnoreDataMember] int Id,
    string Name,
    TimeSpan Duration
    ) : AddServiceRequest(Name, Duration), IId;

public sealed class UpdateServiceCommand(
    IServiceRepository services,
    IETagProvider etagProvider,
    ILogger<UpdateServiceCommand> logger)
    : Abstractions.UpdateCommand<UpdateServiceRequest, Service>(services, logger)
{
    protected override Service Map(UpdateServiceRequest query) =>
        new(query.Name, query.Duration)
        {
            Id = query.Id,
            Version = etagProvider.Version
        };
}
