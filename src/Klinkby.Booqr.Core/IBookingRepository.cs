namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a booking associated with a <see cref="CalendarEvent"/> and a customer.
/// </summary>
/// <remarks>
///     This record is immutable and inherits basic audit properties from the <see cref="Audit" /> base type.
///     A booking links a customer to a service and is typically associated with a calendar event.
/// </remarks>
/// <param name="CustomerId">The unique identifier of the customer making the booking.</param>
/// <param name="ServiceId">The unique identifier of the service being booked.</param>
/// <param name="Notes">Optional notes or comments about the booking.</param>
public sealed record Booking(
    int CustomerId,
    int ServiceId,
    string? Notes
) : Audit;

/// <summary>
///     Provides data access operations for <see cref="Booking"/> entities.
/// </summary>
public interface IBookingRepository : IRepository<Booking>
{
}
