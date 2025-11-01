namespace Klinkby.Booqr.Core;

public sealed record BookingDetails(
    int Id,
    DateTime StartTime,
    string Service,
    TimeSpan Duration,
    string Location,
    string? Employee,
    string? CustomerName,
    string CustomerEmail);

public interface IBookingDetailsRepository : IRepository
{
    IAsyncEnumerable<BookingDetails> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery, CancellationToken cancellation = default);
}
