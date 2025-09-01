using Klinkby.Booqr.Application.Services;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapService(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("/service")
            .WithTags("Service");

        group.MapGet("",
                static (GetServicesCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithSummary("List all services");

        group.MapGet("{id}",
                static (GetServiceByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.GetSingle(request, cancellation))
            .WithSummary("Get a service");

        group.MapPost("",
                static (AddServiceCommand command,
                        [FromBody] AddServiceRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.Created(request, user, "service", cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithSummary("Add a service");

        group.MapPut("{id}",
                static (UpdateServiceCommand command,
                        int id,
                        [FromBody] UpdateServiceRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request with { Id = id }, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithSummary("Update a service");

        group.MapDelete("{id}",
                static (DeleteServiceCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithSummary("Delete a service");
    }
}
