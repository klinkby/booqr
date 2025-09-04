using Klinkby.Booqr.Application.Bookings;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapBookings(IEndpointRouteBuilder app)
    {
        const string resourceName = "bookings";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags("Booking");
        //
        // group.MapGet("",
        //         static (GetLocationCollectionCommand command,
        //                 [AsParameters] PageQuery request,
        //                 CancellationToken cancellation) =>
        //             command.GetCollection(request, cancellation))
        //     .WithSummary("List all locations");
        //
        // group.MapGet("{id}",
        //         static (GetLocationByIdCommand command,
        //                 int id,
        //                 CancellationToken cancellation) =>
        //             command.GetSingle(id, cancellation))
        //     .WithSummary("Get a single location");

        // group.MapPost("",
        //         static (AddBookingCommand command,
        //                 [FromBody] AddBookingRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.Created(request, user, resourceName, cancellation))
        //     .RequireAuthorization(UserRole.Customer)
        //     .WithSummary("Add a booking");

        // group.MapPut("{id}",
        //         static (UpdateLocationCommand command,
        //                 int id,
        //                 [FromBody] UpdateLocationRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .RequireAuthorization(UserRole.Admin)
        //     .WithSummary("Update a location");

        group.MapDelete("{id}",
                static (DeleteBookingCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user,
                        CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Customer)
            .WithSummary("Delete a booking");
    }
}
