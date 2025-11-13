namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a user's booking view, containing booking details and associated entities.
/// </summary>
/// <remarks>
///     This record is immutable and inherits basic audit properties from the <see cref="Audit" /> base type.
///     It implements <see cref="IEvent"/> to provide time range information for the booking.
/// </remarks>
/// <param name="StartTime">The start time of the booking.</param>
/// <param name="EndTime">The end time of the booking.</param>
/// <param name="ServiceId">The identifier of the booked service.</param>
/// <param name="LocationId">The identifier of the location where the service is provided.</param>
/// <param name="EmployeeId">The identifier of the employee assigned to the booking.</param>
/// <param name="CustomerId">The identifier of the customer who made the booking.</param>
/// <param name="HasNotes">Indicates whether the booking has associated notes.</param>
public sealed record MyBooking(
    DateTime StartTime,
    DateTime EndTime,
    int ServiceId,
    int LocationId,
    int EmployeeId,
    int CustomerId,
    bool HasNotes) : Audit, IEvent;

/// <summary>
///     Provides data access operations for user-specific booking views.
/// </summary>
public interface IMyBookingRepository : IRepository
{
    /// <summary>
    ///     Retrieves a range of bookings for a specific user within the specified time period.
    /// </summary>
    /// <param name="userId">The identifier of the user whose bookings to retrieve.</param>
    /// <param name="fromTime">The start of the time range.</param>
    /// <param name="toTime">The end of the time range.</param>
    /// <param name="pageQuery">The pagination parameters.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream of <see cref="MyBooking"/> instances.</returns>
    IAsyncEnumerable<MyBooking> GetRangeByUserId(int userId, DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        CancellationToken cancellation = default);

    /// <summary>
    ///     Retrieves a specific booking by its identifier.
    /// </summary>
    /// <param name="bookingId">The unique identifier of the booking.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the <see cref="MyBooking"/>
    ///     if found, otherwise <c>null</c>.
    /// </returns>
    Task<MyBooking?> GetById(int bookingId, CancellationToken cancellation = default);
}
