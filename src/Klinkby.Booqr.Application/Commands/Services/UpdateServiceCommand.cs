using System.Runtime.Serialization;

namespace Klinkby.Booqr.Application.Commands.Services;

public sealed record UpdateServiceRequest(
    [property: IgnoreDataMember] int Id,
    string Name,
    TimeSpan Duration
    ) : AddServiceRequest(Name, Duration), IId;

public sealed class UpdateServiceCommand(
    IServiceRepository services,
    IRequestMetadata etagProvider,
    IActivityRecorder activityRecorder,
    ILogger<UpdateServiceCommand> logger)
    : UpdateCommand<UpdateServiceRequest, Service>(services, activityRecorder, logger)
{
    protected override Service Map(UpdateServiceRequest query) =>
        new(query.Name, query.Duration)
        {
            Id = query.Id,
            Version = etagProvider.Version
        };
}
