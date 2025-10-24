using Klinkby.Booqr.Application.Commands.Users;

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

        group.MapPost("/reset-password",
                static (ResetPasswordCommand command, [FromBody] ResetPasswordRequest request, CancellationToken cancellation) =>
                command.NoContent(request, cancellation))
            .WithName("resetPassword")
            .WithSummary("Reset password");

        group.MapPost("{id}/change-password",
                static (ChangePasswordCommand command, [FromBody]
                        ChangePasswordRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("changePassword")
            .WithSummary("Change password");

        group.MapGet("{id}/my-bookings",
                static (GetMyBookingsCommand command,
                        [AsParameters] GetMyBookingsRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.GetCollection(request with { User = user }, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("getMyBookings")
            .WithSummary("List my bookings");

        group.MapGet("{id}/my-bookings/{bookingId}",
                static (GetMyBookingByIdCommand command,
                        [AsParameters] GetMyBookingByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.Execute(request with { User = user }, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .AddEndpointFilter<ETagProviderEndPointFilter>()
            .WithName("getMyBookingById")
            .WithSummary("Get a single my booking item");

        group.MapGet("",
                static (GetUserCollectionCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .RequireAuthorization(UserRole.Employee)
            .WithName("getUsers")
            .WithSummary("List users");

        group.MapGet("{id}",
                static (GetUserByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation))
            .AddEndpointFilter<ETagProviderEndPointFilter>()
            .RequireAuthorization(UserRole.Employee)
            .WithName("getUserById")
            .WithSummary("Get a single user");

        group.MapPost("",
                static (SignUpCommand command,
                        [FromBody] SignUpRequest request,
                        CancellationToken cancellation) =>
                    command.CreatedAnonymous(request, $"{BaseUrl}/{resourceName}", cancellation))
            .WithName("addUser")
            .WithSummary("Sign up for a user account");

        // group.MapPut("{id}",
        //         static (UpdateUserCommand command,
        //                 int id,
        //                 [FromBody] UpdateUserRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .AddEndpointFilter<ETagProviderEndPointFilter>()
        //     .RequireAuthorization(UserRole.Admin)
        //     .WithName("updateUser")
        //     .WithSummary("Update a user");
        //
        // group.MapDelete("{id}",
        //         static (DeleteUserCommand command,
        //                 [AsParameters] AuthenticatedByIdRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request, user, cancellation))
        //     .RequireAuthorization(UserRole.Admin)
        //     .WithName("deleteUser")
        //     .WithSummary("Delete a user");
    }
}
