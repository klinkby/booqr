using Klinkby.Booqr.Application.Calendar;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapCalendar(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("/calendar")
            .WithTags("Calendar");

        group.MapGet("",
                static (GetAvailableEventsCommand command,
                        [AsParameters] GetAvailableEventsRequest request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithSummary("List available Events");

        group.MapGet("{id}",
                static (GetEventCommand command, [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.GetSingle(request, cancellation))
            .WithSummary("Get a single Event");

        group.MapDelete("{id}",
                static (DeleteEventCommand command,
                        int id,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(id, user, cancellation))
            .RequireAuthorization(UserRole.Employee)
            .WithSummary("Delete a Event");
    }
}
