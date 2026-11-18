using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.Bookings;

/// <summary>
///     Gets a single booking including its notes. Staff (employees/admins) may view any booking;
///     a customer may only view their own.
/// </summary>
public sealed partial class GetBookingByIdCommand(
    IBookingRepository bookings,
    ILogger<GetBookingByIdCommand> logger) : ICommand<AuthenticatedByIdRequest, Task<Result<Booking>>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<Booking>> Execute(AuthenticatedByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        Booking? booking = await bookings.GetById(query.Id, cancellation);
        if (booking is null)
            return Problem.NotFound with { Detail = $"Booking {query.Id} was not found" };

        if (!query.IsStaffOrOwner(booking.CustomerId))
        {
            _log.CannotViewBooking(query.AuthenticatedUserId, query.Id);
            return Problem.Forbidden with { Detail = "You cannot inspect another customer's booking" };
        }

        return booking;
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(111, LogLevel.Warning, "User {UserId} is not permitted to view booking {Id}")]
        public partial void CannotViewBooking(int userId, int id);
    }
}
