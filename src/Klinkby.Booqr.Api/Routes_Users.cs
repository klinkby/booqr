using Klinkby.Booqr.Application.Users;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapUsers(IEndpointRouteBuilder app)
    {
        const string resourceName = "users";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags("User")
            .WithDescription("Operations related to users");

        group.MapPost("/login",
                static (LoginCommand command, [FromBody] LoginRequest request, CancellationToken cancellation) =>
                command.GetAuthenticationToken(request, cancellation))
            .WithName("login")
            .WithSummary("Sign in");

        group.MapGet("{id}/my-bookings",
                static (GetMyBookingsCommand command,

                        [AsParameters] GetMyBookingsRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.GetCollection(request with { User = user }, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("getMyBookings")
            .WithSummary("List my bookings");
    }
}
