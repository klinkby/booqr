namespace Klinkby.Booqr.Core;

/// <summary>
/// Represents the details of a booking including information about the
/// booking identifier, start time, service, duration, location, employee,
/// customer name, and customer email.
/// </summary>
public sealed record BookingDetails(
    int Id,
    DateTime StartTime,
    string Service,
    TimeSpan Duration,
    string Location,
    string? Employee,
    int CustomerId,
    string? CustomerName,
    string CustomerEmail);

public interface IBookingDetailsRepository : IRepository
{
    IAsyncEnumerable<BookingDetails> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery, CancellationToken cancellation = default);
}
