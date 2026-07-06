using System.Net.Mime;
using Klinkby.Booqr.Application.Commands.Employees;
using Klinkby.Booqr.Application.Models;
using Klinkby.Booqr.Api.Util;

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
            .AddEndpointFilter<RequestMetadataEndPointFilter>()
            .AddEndpointFilter<AuthenticatedRequestEndPointFilter>();
        MapOpenApi(baseRoute);
        MapAuth(baseRoute);
        MapBookings(baseRoute);
        MapEmployees(baseRoute);
        MapLocations(baseRoute);
        MapServices(baseRoute);
        MapUsers(baseRoute);
        MapVacancies(baseRoute);
    }

    private static void MapOpenApi(RouteGroupBuilder baseRoute)
    {
        const string filename = "v1.json";
        baseRoute.MapGet(filename,
                static (HttpContext context, IWebHostEnvironment env, CancellationToken cancellation) =>
                {
                    HttpResponse response = context.Response;
                    response.ContentType = MediaTypeNames.Application.Json;
                    response.Headers.CacheControl = "public, max-age=86400";
                    return response.SendFileAsync(Path.Combine(env.ContentRootPath, "openapi", filename), cancellation);
                })
            .ExcludeFromDescription();
    }

    private static void MapAuth(IEndpointRouteBuilder app)
    {
        const string resourceName = "auth";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags("Authentication")
            .WithDescription("Authentication");

        group.MapPost("/login",
                static (LoginCommand command, [FromBody] LoginRequest request, HttpContext context,
                    CancellationToken cancellation) => command
                    .Execute(request.WithRefreshToken(context), cancellation)
                    .ToOk(x => x.AddRefreshTokenCookie(context)))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("login")
            .WithSummary("Sign in");

        group.MapPost("/refresh",
                static (RefreshCommand command, HttpContext context, CancellationToken cancellation) => command
                    .Execute(new RefreshRequest().WithRefreshToken(context), cancellation)
                    .ToOk(x => x.AddRefreshTokenCookie(context)))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("refresh")
            .WithSummary("Refresh auth token");

        group.MapPost("/logout",
                static (LogoutCommand command, HttpContext context, CancellationToken cancellation) => command
                    .Execute(new LogoutRequest().WithRefreshToken(context), cancellation)
                    .ToOk(x => x.DeleteRefreshTokenCookie(context)))
            .WithName("logout")
            .WithSummary("Log out");
    }

    private static void MapBookings(IEndpointRouteBuilder app)
    {
        const string resourceName = "bookings";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags(nameof(Booking))
            .WithDescription(nameof(Booking));

        group.MapGet(IdRoutePattern, static ([AsParameters] AuthenticatedByIdRequest request) =>
            TypedResults.LocalRedirect($"/api/users/{request.AuthenticatedUserId}/my-bookings/{request.Id}"))
            .RequireAuthorization(UserRole.Customer)
            .Produces(StatusCodes.Status302Found)
            .ProducesValidationProblem()
            .WithName("getBookingById")
            .WithSummary("Get a single booking");

        group.MapPost("",
                static (AddBookingCommand command,
                    [FromBody] AddBookingRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToCreated(resourceName))
            .RequireAuthorization(UserRole.Customer)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status201Created)
            .WithName("addBooking")
            .WithSummary("Add a booking");

        // group.MapPut(IdRoutePattern,
        //         static (UpdateLocationCommand command,
        //                 int id,
        //                 [FromBody] UpdateLocationRequest request,
        //                 CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .RequireAuthorization(UserRole.Admin)
        //     .WithName("").WithSummary("Update a location");

        group.MapDelete(IdRoutePattern,
                static (DeleteBookingCommand command,
                    [AsParameters] AuthenticatedByIdRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToNoContent())
            .RequireAuthorization(UserRole.Customer)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("deleteBooking")
            .WithSummary("Delete a booking");
    }

    private static void MapEmployees(IEndpointRouteBuilder app)
    {
        const string resourceName = "employees";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags(nameof(Employee))
            .WithDescription(nameof(Employee));

        group.MapGet("",
                static (GetEmployeeCollectionCommand command,
                        CancellationToken cancellation) =>
                    command.Execute(new PageQuery(), cancellation).ToOk())
            .WithName("getEmployees")
            .WithSummary("List employees");
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
                    command.Execute(request, cancellation).ToOk())
            .WithName("getLocations")
            .WithSummary("List locations");

        group.MapGet(IdRoutePattern,
                static (GetLocationByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation).ToOk())
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("getLocationById")
            .WithSummary("Get a single location");

        group.MapPost("",
                static (AddLocationCommand command,
                    [FromBody] AddLocationRequest request, CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToCreated(resourceName))
            .RequireAuthorization(UserRole.Admin)
            .ProducesValidationProblem()
            .WithName("addLocation")
            .WithSummary("Add a location");

        group.MapPut(IdRoutePattern,
                static (UpdateLocationCommand command,
                    int id,
                    [FromBody] UpdateLocationRequest request, CancellationToken cancellation) => command
                    .Execute(request with { Id = id }, cancellation)
                    .ToNoContent())
            .RequireAuthorization(UserRole.Admin)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .WithName("updateLocation")
            .WithSummary("Update a location");

        group.MapDelete(IdRoutePattern,
                static (DeleteLocationCommand command,
                    [AsParameters] AuthenticatedByIdRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToNoContent())
            .RequireAuthorization(UserRole.Admin)
            .ProducesValidationProblem()
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
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToOk())
            .WithName("getServices")
            .WithSummary("List services");

        group.MapGet(IdRoutePattern,
                static (GetServiceByIdCommand command,
                    [AsParameters] ByIdRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToOk())
            .ProducesValidationProblem()
            .WithName("getServiceById")
            .WithSummary("Get a single service");

        group.MapPost("",
                static (AddServiceCommand command,
                    [FromBody] AddServiceRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToCreated(resourceName))
            .RequireAuthorization(UserRole.Admin)
            .ProducesValidationProblem()
            .WithName("addService")
            .WithSummary("Add a service");

        group.MapPut(IdRoutePattern,
                static (UpdateServiceCommand command,
                    int id,
                    [FromBody] UpdateServiceRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request with { Id = id }, cancellation)
                    .ToNoContent())
            .RequireAuthorization(UserRole.Admin)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .WithName("updateService")
            .WithSummary("Update a service");

        group.MapDelete(IdRoutePattern,
                static (DeleteServiceCommand command,
                    [AsParameters] AuthenticatedByIdRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToNoContent())
            .RequireAuthorization(UserRole.Admin)
            .ProducesValidationProblem()
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

        group.MapPost("/reset-password",
                static (ResetPasswordCommand command, [FromBody] ResetPasswordRequest request, HttpContext context,
                    CancellationToken cancellation) => command
                    .Execute(request with { Authority = context.GetContextAuthority() }, cancellation)
                    .ToNoContent())
            .ProducesValidationProblem()
            .WithName("resetPassword")
            .WithSummary("Reset password");

        group.MapPost("/change-password",
                static (ChangePasswordCommand command,
                    [FromBody] ChangePasswordRequest request,
                    HttpContext context,
                    CancellationToken cancellation) => command
                    .Execute(request with { QueryString = context.Request.QueryString.Value ?? "" }, cancellation)
                    .ToNoContent())
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .WithName("changePassword")
            .WithSummary("Change password");

        group.MapGet($"{IdRoutePattern}/my-bookings",
                static (GetMyBookingsCommand command,
                    [AsParameters] GetMyBookingsRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToOk(x => new CollectionResponse<MyBooking>(x)))
            .RequireAuthorization(UserRole.Customer)
            .ProducesValidationProblem()
            .WithName("getMyBookings")
            .WithSummary("List my bookings");

        group.MapGet($"{IdRoutePattern}/my-bookings/{BookingIdRoutePattern}",
                static (GetMyBookingByIdCommand command,
                    [AsParameters] GetMyBookingByIdRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToOk())
            .RequireAuthorization(UserRole.Customer)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithName("getMyBookingById")
            .WithSummary("Get a single my booking item");

        group.MapGet("",
                static (GetUserCollectionCommand command,
                    [AsParameters] GetUserCollectionRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToOk())
            .RequireAuthorization(UserRole.Employee)
            .ProducesValidationProblem()
            .WithName("getUsers")
            .WithSummary("List users");

        group.MapGet(IdRoutePattern,
                static (GetUserByIdCommand command,
                    [AsParameters] ByIdRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToOk())
            .RequireAuthorization(UserRole.Employee)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("getUserById")
            .WithSummary("Get a single user");

        group.MapPost("",
                static (SignUpCommand command,
                    [FromBody] SignUpRequest request,
                    HttpContext context,
                    CancellationToken cancellation) => command
                    .Execute(request with { Authority = context.GetContextAuthority() }, cancellation)
                    .ToCreated(resourceName))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("addUser")
            .WithSummary("Sign up for a user account");

        group.MapPut(IdRoutePattern,
                static (UpdateUserProfileCommand command,
                    int id,
                    [FromBody] UpdateUserProfileRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request with { Id = id }, cancellation)
                    .ToNoContent())
            .RequireAuthorization(UserRole.Customer)
            .ProducesValidationProblem()
            .WithName("updateUser")
            .WithSummary("Update a user");

        group.MapDelete(IdRoutePattern,
                static (DeleteUserCommand command,
                    [AsParameters] AuthenticatedByIdRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToNoContent())
            .RequireAuthorization(UserRole.Admin)
            .WithName("deleteUser")
            .WithSummary("Delete a user");
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
                    command.Execute(request, cancellation).ToOk())
            .WithName("getVacancies")
            .WithSummary("List vacancies");

        group.MapGet(IdRoutePattern,
                static (GetVacancyByIdCommand command,
                        [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    command.Execute(request, cancellation).ToOk())
            .WithName("getVacancyById")
            .WithSummary("Get a single vacancy");

        group.MapPost("",
                static (AddVacancyCommand command,
                    [FromBody] AddVacancyRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToCreated(resourceName))
            .RequireAuthorization(UserRole.Employee)
            .WithName("addVacancy")
            .WithSummary("Add a vacancy");

        // group.MapPut("{id:int}",
        //         static (UpdateVacancyCommand command,
        //                 int id,
        //                 [FromBody] UpdateVacancyRequest request,
        //                 CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .RequireAuthorization(UserRole.Employee)
        //     .WithName().WithSummary("Update a vacancy");
        //
        group.MapDelete(IdRoutePattern,
                static (DeleteVacancyCommand command,
                    [AsParameters] AuthenticatedByIdRequest request,
                    CancellationToken cancellation) => command
                    .Execute(request, cancellation)
                    .ToNoContent())
            .RequireAuthorization(UserRole.Employee)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("deleteVacancy")
            .WithSummary("Delete a vacancy");
    }
}

file static class ResultMapperExtensions
{

    private static CookieOptions CreateRefreshTokenCookieOptions(DateTimeOffset? expires = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = expires
        };
    }

    internal static bool DeleteRefreshTokenCookie(this bool result, HttpContext context)
    {
        context.Response.Cookies.Delete(CommandExtensions.RefreshTokenCookieName, CreateRefreshTokenCookieOptions());
        return result;
    }

    internal static OAuthTokenResponse AddRefreshTokenCookie(this OAuthTokenResponse response, HttpContext context)
    {
        context.Response.Cookies.Append(CommandExtensions.RefreshTokenCookieName, response.RefreshToken!,
            CreateRefreshTokenCookieOptions(response.RefreshTokenExpiration));
        context.Response.Headers.CacheControl = "no-store";
        return response with { RefreshToken = string.Empty };
    }
}
