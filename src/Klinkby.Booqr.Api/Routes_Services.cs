using Klinkby.Booqr.Application.Commands.Services;
using Klinkby.Booqr.Application.Services;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapServices(IEndpointRouteBuilder app)
    {
        const string resourceName = "services";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags("Service")
            .WithDescription("Operations related to services");

        group.MapGet("",
                static (GetServiceCollectionCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithName("getServices")
            .WithSummary("List services");

        group.MapGet("{id}",
                static (GetServiceByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation))
            .AddEndpointFilter<ETagProviderEndPointFilter>()
            .WithName("getServiceById")
            .WithSummary("Get a single service");

        group.MapPost("",
                static (AddServiceCommand command,
                        [FromBody] AddServiceRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.Created(request, user, $"{BaseUrl}/{resourceName}", cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithName("addService")
            .WithSummary("Add a service");

        group.MapPut("{id}",
                static (UpdateServiceCommand command,
                        int id,
                        [FromBody] UpdateServiceRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request with { Id = id }, user, cancellation))
            .AddEndpointFilter<ETagProviderEndPointFilter>()
            .RequireAuthorization(UserRole.Admin)
            .WithName("updateService")
            .WithSummary("Update a service");

        group.MapDelete("{id}",
                static (DeleteServiceCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithName("deleteService")
            .WithSummary("Delete a service");
    }
}
