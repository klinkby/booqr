namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a calendar event associated with an employee, encompassing a defined time interval and optional notes.
/// </summary>
/// <remarks>
///     This record is immutable and inherits basic audit properties from the <see cref="Audit" /> base type.
/// </remarks>
public sealed record CalendarEvent(
    int EmployeeId,
    int LocationId,
    int? BookingId,
    DateTime StartTime,
    DateTime EndTime
) : Audit, IEvent;

public interface IEvent
{
    DateTime StartTime { get; }
    DateTime EndTime { get; }
}

public interface ICalendarRepository : IRepository<CalendarEvent>
{
    IAsyncEnumerable<CalendarEvent> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        bool available, bool booked,
        CancellationToken cancellation = default);

    Task<CalendarEvent?> GetByBookingId(int bookingId, CancellationToken cancellation = default);
}
