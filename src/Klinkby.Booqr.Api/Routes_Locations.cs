using Klinkby.Booqr.Application.Locations;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapLocations(IEndpointRouteBuilder app)
    {
        const string resourceName = "locations";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags("Location")
            .WithDescription("Operations related to locations");

        group.MapGet("",
                static (GetLocationCollectionCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithName("getLocations")
            .WithSummary("List locations");

        group.MapGet("{id}",
                static (GetLocationByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation))
            .AddEndpointFilter<ETagProviderEndPointFilter>()
            .WithName("getLocationById")
            .WithSummary("Get a single location");

        group.MapPost("",
                static (AddLocationCommand command,
                        [FromBody] AddLocationRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.Created(request, user, $"{BaseUrl}/{resourceName}", cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithName("addLocation")
            .WithSummary("Add a location");

        group.MapPut("{id}",
                static (UpdateLocationCommand command,
                        int id,
                        [FromBody] UpdateLocationRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request with { Id = id }, user, cancellation))
            .AddEndpointFilter<ETagProviderEndPointFilter>()
            .RequireAuthorization(UserRole.Admin)
            .WithName("updateLocation")
            .WithSummary("Update a location");

        group.MapDelete("{id}",
                static (DeleteLocationCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithName("deleteLocation")
            .WithSummary("Delete a location");
    }
}
