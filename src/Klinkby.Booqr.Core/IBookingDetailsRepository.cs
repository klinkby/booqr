namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents the details of a booking, aggregating information from multiple entities.
/// </summary>
/// <remarks>
///     This record provides a read-only view of booking information by combining data
///     from bookings, services, locations, employees, and customers.
/// </remarks>
/// <param name="Id">The unique identifier of the booking.</param>
/// <param name="StartTime">The start date and time of the booking.</param>
/// <param name="Service">The name of the booked service.</param>
/// <param name="Duration">The duration of the service.</param>
/// <param name="Location">The name of the location where the service is provided.</param>
/// <param name="Employee">The name of the employee providing the service, or <c>null</c> if not assigned.</param>
/// <param name="CustomerId">The unique identifier of the customer.</param>
/// <param name="CustomerName">The name of the customer, or <c>null</c> if not available.</param>
/// <param name="CustomerEmail">The email address of the customer.</param>
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

/// <summary>
///     Provides data access operations for <see cref="BookingDetails"/> views.
/// </summary>
/// <remarks>
///     This repository provides read-only access to booking details, which aggregate information
///     from multiple entities including bookings, services, locations, employees, and customers.
/// </remarks>
public interface IBookingDetailsRepository : IRepository
{
    /// <summary>
    ///     Retrieves booking details within the specified time range.
    /// </summary>
    /// <param name="fromTime">The start of the time range.</param>
    /// <param name="toTime">The end of the time range.</param>
    /// <param name="pageQuery">The pagination parameters.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream of <see cref="BookingDetails"/> instances.</returns>
    IAsyncEnumerable<BookingDetails> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery, CancellationToken cancellation = default);
}
