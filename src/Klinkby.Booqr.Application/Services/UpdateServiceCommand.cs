namespace Klinkby.Booqr.Application.Services;

public sealed record UpdateServiceRequest(
    int Id,
    string Name,
    TimeSpan Duration
    ) : AddServiceRequest(Name, Duration), IId;

public sealed class UpdateServiceCommand(
    IServiceRepository services,
    ILogger<UpdateServiceCommand> logger)
    : UpdateCommand<UpdateServiceRequest, Service>(services, logger)
{
    protected override Service Map(UpdateServiceRequest query) =>
        new(query.Name, query.Duration) { Id = query.Id };
}
