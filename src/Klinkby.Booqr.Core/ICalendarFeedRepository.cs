namespace Klinkby.Booqr.Core;

/// <summary>
///     A booking as published in a user's iCalendar feed: only what the calendar entry needs,
///     never the other party's name, email or the booking notes.
/// </summary>
/// <param name="Id">The booking id (stable, used to build the event UID); <c>long</c> because <c>bookings.id</c> is <c>bigint</c>.</param>
/// <param name="StartTime">Start of the appointment (UTC).</param>
/// <param name="EndTime">End of the appointment (UTC).</param>
/// <param name="Service">The booked service's name.</param>
/// <param name="Location">The location's name.</param>
/// <param name="Address1">Location address line 1.</param>
/// <param name="Address2">Location address line 2.</param>
/// <param name="Zip">Location postal code.</param>
/// <param name="City">Location city.</param>
/// <param name="Modified">When the booking or its calendar slot was last modified (UTC).</param>
public sealed record CalendarFeedEvent(
    long Id,
    DateTime StartTime,
    DateTime EndTime,
    string Service,
    string Location,
    string? Address1,
    string? Address2,
    string? Zip,
    string? City,
    DateTime Modified);

/// <summary>
///     Read model for the iCalendar booking feed.
/// </summary>
public interface ICalendarFeedRepository : IRepository
{
    /// <summary>
    ///     Returns the non-deleted bookings where <paramref name="userId" /> is the customer or the assigned
    ///     employee and that overlap [<paramref name="fromTime" />, <paramref name="toTime" />], ordered by start time.
    ///     At most <paramref name="maxEvents" /> are returned.
    /// </summary>
    IAsyncEnumerable<CalendarFeedEvent> GetEvents(int userId, DateTime fromTime, DateTime toTime, int maxEvents,
        CancellationToken cancellation = default);
}
