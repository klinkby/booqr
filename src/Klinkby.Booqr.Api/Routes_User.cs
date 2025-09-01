using Klinkby.Booqr.Application.Users;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapUser(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("/user")
            .WithTags("User");

        group.MapPost("/login",
                static (LoginCommand command, [FromBody] LoginRequest request, CancellationToken cancellation) =>
                command.GetAuthenticationToken(request, cancellation))
            .WithSummary("Sign in with username (email) and password");
    }
}
