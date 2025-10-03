using Klinkby.Booqr.Application.Bookings;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
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
        group.MapGet("{id}", static ([AsParameters] AuthenticatedByIdRequest request, ClaimsPrincipal user) =>
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

        // group.MapPut("{id}",
        //         static (UpdateLocationCommand command,
        //                 int id,
        //                 [FromBody] UpdateLocationRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .AddEndpointFilter<ETagProviderEndPointFilter>()
        //     .RequireAuthorization(UserRole.Admin)
        //     .AddEndpointFilter<ETagProviderEndPointFilter>()
        //     .WithName("").WithSummary("Update a location");

        group.MapDelete("{id}",
                static (DeleteBookingCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithName("deleteBooking")
            .WithSummary("Delete a booking");
    }
}
