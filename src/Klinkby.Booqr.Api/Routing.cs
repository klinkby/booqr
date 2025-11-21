using Klinkby.Booqr.Application.Models;

namespace Klinkby.Booqr.Api;

internal static class Routing
{
    private const string BaseUrl = "/api";
    private const string IdRoutePattern = "{id:int}";
    private const string BookingIdRoutePattern = "{bookingId:int}";

    internal static void MapApiRoutes(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder baseRoute = app
            .MapGroup(BaseUrl)
            .AddEndpointFilter<RequestMetadataEndPointFilter>();

        MapBookings(baseRoute);
        MapLocations(baseRoute);
        MapServices(baseRoute);
        MapUsers(baseRoute);
        MapVacancies(baseRoute);
    }

    private static void MapBookings(IEndpointRouteBuilder app)
    {
        const string resourceName = "bookings";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags(nameof(Booking))
            .WithDescription(nameof(Booking));

        group.MapGet(IdRoutePattern, static ([AsParameters] AuthenticatedByIdRequest request, ClaimsPrincipal user) =>
            TypedResults.LocalRedirect($"/api/users/{(request with { User = user }).AuthenticatedUserId}/my-bookings/{request.Id}"))
            .RequireAuthorization(UserRole.Customer)
            .WithName("getBookingById")
            .WithSummary("Get a single booking");

        group.MapPost("",
                static (AddBookingCommand command,
                        [FromBody] AddBookingRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.Created(request, user, $"{BaseUrl}/{resourceName}", cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("addBooking")
            .WithSummary("Add a booking");

        // group.MapPut(IdRoutePattern,
        //         static (UpdateLocationCommand command,
        //                 int id,
        //                 [FromBody] UpdateLocationRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .RequireAuthorization(UserRole.Admin)
        //     .WithName("").WithSummary("Update a location");

        group.MapDelete(IdRoutePattern,
                static (DeleteBookingCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("deleteBooking")
            .WithSummary("Delete a booking");
    }

        private static void MapLocations(IEndpointRouteBuilder app)
    {
        const string resourceName = "locations";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags(nameof(Location))
            .WithDescription(nameof(Location));

        group.MapGet("",
                static (GetLocationCollectionCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithName("getLocations")
            .WithSummary("List locations");

        group.MapGet(IdRoutePattern,
                static (GetLocationByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.GetSingle(request, cancellation))
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

        group.MapPut(IdRoutePattern,
                static (UpdateLocationCommand command,
                        int id,
                        [FromBody] UpdateLocationRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request with { Id = id }, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithName("updateLocation")
            .WithSummary("Update a location");

        group.MapDelete(IdRoutePattern,
                static (DeleteLocationCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithName("deleteLocation")
            .WithSummary("Delete a location");
    }

    private static void MapServices(IEndpointRouteBuilder app)
    {
        const string resourceName = "services";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags(nameof(Service))
            .WithDescription(nameof(Service));

        group.MapGet("",
                static (GetServiceCollectionCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithName("getServices")
            .WithSummary("List services");

        group.MapGet(IdRoutePattern,
                static (GetServiceByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.GetSingle(request, cancellation))
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

        group.MapPut(IdRoutePattern,
                static (UpdateServiceCommand command,
                        int id,
                        [FromBody] UpdateServiceRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request with { Id = id }, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithName("updateService")
            .WithSummary("Update a service");

        group.MapDelete(IdRoutePattern,
                static (DeleteServiceCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Admin)
            .WithName("deleteService")
            .WithSummary("Delete a service");
    }

    private static void MapUsers(IEndpointRouteBuilder app)
    {
        const string resourceName = "users";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags(nameof(User))
            .WithDescription(nameof(User));

        group.MapPost("/login",
                static (LoginCommand command, [FromBody] LoginRequest request, CancellationToken cancellation) =>
                command.GetAuthenticationToken(request, cancellation))
            .WithName("login")
            .WithSummary("Sign in");

        group.MapPost("/reset-password",
                static (ResetPasswordCommand command, [FromBody] ResetPasswordRequest request, HttpContext context, CancellationToken cancellation) =>
                command.NoContent(request with { Authority = context.GetContextAuthority() }, cancellation))
            .WithName("resetPassword")
            .WithSummary("Reset password");

        group.MapPost("/change-password",
                static (ChangePasswordCommand command,
                        [FromBody] ChangePasswordRequest request,
                        HttpContext context,
                        CancellationToken cancellation) =>
                command.NoContent(request with { QueryString = context.Request.QueryString.Value ?? "" }, cancellation))
            .WithName("changePassword")
            .WithSummary("Change password");

        group.MapGet($"{IdRoutePattern}/my-bookings",
                static (GetMyBookingsCommand command,
                        [AsParameters] GetMyBookingsRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.GetCollection(request with { User = user }, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("getMyBookings")
            .WithSummary("List my bookings");

        group.MapGet($"{IdRoutePattern}/my-bookings/{BookingIdRoutePattern}",
                static (GetMyBookingByIdCommand command,
                        [AsParameters] GetMyBookingByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.GetSingle(request with { User = user }, cancellation))
            .RequireAuthorization(UserRole.Customer)
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

        group.MapGet(IdRoutePattern,
                static (GetUserByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.GetSingle(request, cancellation))
            .RequireAuthorization(UserRole.Employee)
            .WithName("getUserById")
            .WithSummary("Get a single user");

        group.MapPost("",
                static (SignUpCommand command,
                        [FromBody] SignUpRequest request,
                        HttpContext context,
                        CancellationToken cancellation) =>
                    command.CreatedAnonymous(request with { Authority = context.GetContextAuthority() }, $"{BaseUrl}/{resourceName}", cancellation))
            .WithName("addUser")
            .WithSummary("Sign up for a user account");

        // group.MapPut(IdRoutePattern,
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
        // group.MapDelete(IdRoutePattern,
        //         static (DeleteUserCommand command,
        //                 [AsParameters] AuthenticatedByIdRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request, user, cancellation))
        //     .RequireAuthorization(UserRole.Admin)
        //     .WithName("deleteUser")
        //     .WithSummary("Delete a user");
    }

    private static void MapVacancies(IEndpointRouteBuilder app)
    {
        const string resourceName = "vacancies";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags("Vacancy")
            .WithDescription("Vacancy");

        group.MapGet("",
                static (GetVacancyCollectionCommand command,
                        [AsParameters] GetVacanciesRequest request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithName("getVacancies")
            .WithSummary("List vacancies");

        group.MapGet(IdRoutePattern,
                static (GetVacancyByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.GetSingle(request, cancellation))
            .WithName("getVacancyById")
            .WithSummary("Get a single vacancy");

        group.MapPost("",
                static (AddVacancyCommand command,
                        [FromBody] AddVacancyRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.Created(request, user, $"{BaseUrl}/{resourceName}", cancellation))
            .RequireAuthorization(UserRole.Employee)
            .WithName("addVacancy")
            .WithSummary("Add a vacancy");

        // group.MapPut("{id:int}",
        //         static (UpdateVacancyCommand command,
        //                 int id,
        //                 [FromBody] UpdateVacancyRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .RequireAuthorization(UserRole.Employee)
        //     .WithName().WithSummary("Update a vacancy");
        //
        group.MapDelete(IdRoutePattern,
                static (DeleteVacancyCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Employee)
            .WithName("deleteVacancy")
            .WithSummary("Delete a vacancy");
    }
}
