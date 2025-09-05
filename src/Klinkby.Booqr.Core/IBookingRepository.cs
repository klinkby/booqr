namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a booking associated with a <see cref="CalendarEvent"/> and a customer.
/// </summary>
/// <remarks>
///     This record is immutable and inherits basic audit properties from the <see cref="Audit" /> base type.
/// </remarks>
public sealed record Booking(
    int CustomerId,
    int ServiceId,
    string? Notes
) : Audit;

public interface IBookingRepository : IRepository<Booking>
{
}
