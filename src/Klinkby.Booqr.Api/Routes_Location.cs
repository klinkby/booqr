using Klinkby.Booqr.Application.Locations;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapLocation(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("/location")
            .WithTags("Location");

        group.MapGet("",
                static (GetLocationsCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithSummary("List all locations");

        group.MapGet("{id}",
                static (GetLocationByIdCommand command,
                        int id,
                        CancellationToken cancellation) =>
                    command.GetSingle(id, cancellation))
            .WithSummary("Get a location");

        group.MapPost("",
                static (AddLocationCommand command,
                        [FromBody] AddLocationRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.Created(request, user, "location", cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithSummary("Add a location");

        group.MapPut("{id}",
                static (UpdateLocationCommand command,
                        int id,
                        [FromBody] UpdateLocationRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request with { Id = id }, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithSummary("Update a location");

        group.MapDelete("{id}",
                static (DeleteLocationCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithSummary("Delete a location");
    }
}
