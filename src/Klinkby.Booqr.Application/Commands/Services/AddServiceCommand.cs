namespace Klinkby.Booqr.Application.Commands.Services;

public record AddServiceRequest(
    [property: Required]
    [property: StringLength(0xff)]
    string Name,
    TimeSpan Duration
) : AuthenticatedRequest;

public sealed class AddServiceCommand(
    IServiceRepository services,
    ILogger<AddServiceCommand> logger)
    : Abstractions.AddCommand<AddServiceRequest, Service>(services, logger)
{
    protected override Service Map(AddServiceRequest query) =>
        new(query.Name, query.Duration);
}
