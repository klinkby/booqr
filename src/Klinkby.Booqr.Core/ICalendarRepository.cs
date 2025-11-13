namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a calendar event associated with an employee and location, encompassing a defined time interval.
/// </summary>
/// <remarks>
///     This record is immutable and inherits basic audit properties from the <see cref="Audit" /> base type.
///     Calendar events can represent both available time slots and booked appointments.
/// </remarks>
/// <param name="EmployeeId">The unique identifier of the employee associated with this event.</param>
/// <param name="LocationId">The unique identifier of the location where the event takes place.</param>
/// <param name="BookingId">The unique identifier of the associated booking, or <c>null</c> if this is an available slot.</param>
/// <param name="StartTime">The date and time when the event begins.</param>
/// <param name="EndTime">The date and time when the event ends.</param>
public sealed record CalendarEvent(
    int EmployeeId,
    int LocationId,
    int? BookingId,
    DateTime StartTime,
    DateTime EndTime
) : Audit, IEvent;

/// <summary>
///     Represents an event with a defined time range.
/// </summary>
public interface IEvent
{
    /// <summary>
    ///     Gets the start time of the event.
    /// </summary>
    /// <value>The date and time when the event begins.</value>
    DateTime StartTime { get; }

    /// <summary>
    ///     Gets the end time of the event.
    /// </summary>
    /// <value>The date and time when the event ends.</value>
    DateTime EndTime { get; }
}

/// <summary>
///     Provides data access operations for <see cref="CalendarEvent"/> entities.
/// </summary>
public interface ICalendarRepository : IRepository<CalendarEvent>
{
    /// <summary>
    ///     Retrieves calendar events within the specified time range, filtered by availability and booking status.
    /// </summary>
    /// <param name="fromTime">The start of the time range.</param>
    /// <param name="toTime">The end of the time range.</param>
    /// <param name="pageQuery">The pagination parameters.</param>
    /// <param name="available">If <c>true</c>, includes available slots; otherwise excludes them.</param>
    /// <param name="booked">If <c>true</c>, includes booked slots; otherwise excludes them.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream of <see cref="CalendarEvent"/> instances.</returns>
    IAsyncEnumerable<CalendarEvent> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        bool available, bool booked,
        CancellationToken cancellation = default);

    /// <summary>
    ///     Retrieves a calendar event associated with a specific booking.
    /// </summary>
    /// <param name="bookingId">The unique identifier of the booking.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the <see cref="CalendarEvent"/>
    ///     if found, otherwise <c>null</c>.
    /// </returns>
    Task<CalendarEvent?> GetByBookingId(int bookingId, CancellationToken cancellation = default);
}
