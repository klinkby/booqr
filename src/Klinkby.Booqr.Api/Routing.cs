using Klinkby.Booqr.Application.Models;

namespace Klinkby.Booqr.Api;

internal static class Routing
{
    private const string BaseUrl = "/api";

    public static void MapApiRoutes(this IEndpointRouteBuilder app)
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
            .WithTags("Booking")
            .WithDescription("Operations related to bookings");

        // group.MapGet("",
        //         static (GetBookingCollectionCommand command,
        //                 [AsParameters] GetBookingsRequest request,
        //                 CancellationToken cancellation) =>
        //             command.GetCollection(request, cancellation))
        //     .RequireAuthorization(UserRole.Customer)

        //                 .WithName("").WithSummary("List bookings");
        //
        group.MapGet("{id:int}", static ([AsParameters] AuthenticatedByIdRequest request, ClaimsPrincipal user) =>
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

        // group.MapPut("{id:int}",
        //         static (UpdateLocationCommand command,
        //                 int id,
        //                 [FromBody] UpdateLocationRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .AddEndpointFilter<ETagProviderEndPointFilter>()
        //     .RequireAuthorization(UserRole.Admin)
        //     .AddEndpointFilter<ETagProviderEndPointFilter>()
        //     .WithName("").WithSummary("Update a location");

        group.MapDelete("{id:int}",
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
            .WithTags("Location")
            .WithDescription("Operations related to locations");

        group.MapGet("",
                static (GetLocationCollectionCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithName("getLocations")
            .WithSummary("List locations");

        group.MapGet("{id:int}",
                static (GetLocationByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation))
//            .AddEndpointFilter<ETagProviderEndPointFilter>()
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

        group.MapPut("{id:int}",
                static (UpdateLocationCommand command,
                        int id,
                        [FromBody] UpdateLocationRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request with { Id = id }, user, cancellation))
//            .AddEndpointFilter<ETagProviderEndPointFilter>()
            .RequireAuthorization(UserRole.Admin)
            .WithName("updateLocation")
            .WithSummary("Update a location");

        group.MapDelete("{id:int}",
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
            .WithTags("Service")
            .WithDescription("Operations related to services");

        group.MapGet("",
                static (GetServiceCollectionCommand command,
                        [AsParameters] PageQuery request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithName("getServices")
            .WithSummary("List services");

        group.MapGet("{id:int}",
                static (GetServiceByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation))
//            .AddEndpointFilter<ETagProviderEndPointFilter>()
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

        group.MapPut("{id:int}",
                static (UpdateServiceCommand command,
                        int id,
                        [FromBody] UpdateServiceRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request with { Id = id }, user, cancellation))
//            .AddEndpointFilter<ETagProviderEndPointFilter>()
            .RequireAuthorization(UserRole.Admin)
            .WithName("updateService")
            .WithSummary("Update a service");

        group.MapDelete("{id:int}",
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

        group.MapPost("/change-password",
                static (ChangePasswordCommand command,
                        [FromBody] ChangePasswordRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("changePassword")
            .WithSummary("Change password");

        group.MapGet("{id:int}/my-bookings",
                static (GetMyBookingsCommand command,
                        [AsParameters] GetMyBookingsRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.GetCollection(request with { User = user }, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("getMyBookings")
            .WithSummary("List my bookings");

        group.MapGet("{id:int}/my-bookings/{bookingId:int}",
                static (GetMyBookingByIdCommand command,
                        [AsParameters] GetMyBookingByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.Execute(request with { User = user }, cancellation))
            .RequireAuthorization(UserRole.Customer)
//            .AddEndpointFilter<ETagProviderEndPointFilter>()
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

        group.MapGet("{id:int}",
                static (GetUserByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation))
//            .AddEndpointFilter<ETagProviderEndPointFilter>()
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

        // group.MapPut("{id:int}",
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
        // group.MapDelete("{id:int}",
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
            .WithDescription("Operations related to vacancies");

        group.MapGet("",
                static (GetVacancyCollectionCommand command,
                        [AsParameters] GetVacanciesRequest request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithName("getVacancies")
            .WithSummary("List vacancies");

        group.MapGet("{id:int}",
                static (GetVacancyByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation))
//            .AddEndpointFilter<ETagProviderEndPointFilter>()
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
        group.MapDelete("{id:int}",
                static (DeleteVacancyCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Employee)
            .WithName("deleteVacancy")
            .WithSummary("Delete a vacancy");
    }

}
