namespace Klinkby.Booqr.Application.Services;

public sealed record UpdateServiceRequest(
    [property: Range(1, int.MaxValue)] int Id,
    string Name) : AddServiceRequest(Name), IId;

public sealed class UpdateServiceCommand(
    IServiceRepository services,
    ILogger<UpdateServiceCommand> logger)
    : UpdateCommand<UpdateServiceRequest, Service>(services, logger)
{
    protected override Service Map(UpdateServiceRequest query) =>
        new(query.Name) { Id = query.Id };
}
