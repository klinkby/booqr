namespace Klinkby.Booqr.Application.Commands.Services;

public record AddServiceRequest(
    [property: Required]
    [property: StringLength(0xff)]
    string Name,
    TimeSpan Duration
) : AuthenticatedRequest;

public sealed class AddServiceCommand(
    IServiceRepository services,
    IActivityRecorder activityRecorder,
    ILogger<AddServiceCommand> logger)
    : AddCommand<AddServiceRequest, Service>(services, activityRecorder, logger)
{
    protected override Service Map(AddServiceRequest query) =>
        new(query.Name, query.Duration);
}
