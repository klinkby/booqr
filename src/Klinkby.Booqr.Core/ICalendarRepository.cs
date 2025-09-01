using System.ComponentModel.DataAnnotations;

namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a calendar event associated with an employee, encompassing a defined time interval and optional notes.
/// </summary>
/// <remarks>
///     This record is immutable and inherits basic audit properties from the <see cref="Audit" /> base type.
/// </remarks>
public sealed record CalendarEvent(
    [property: Required]
    [property: Range(1, int.MaxValue)]
    int EmployeeId,
    [property: Required]
    [property: Range(1, int.MaxValue)]
    int LocationId,
    [property: Range(1, int.MaxValue)] int? BookingId,
    DateTime StartTime,
    DateTime EndTime
) : Audit;

public interface ICalendarRepository : IRepository<CalendarEvent>
{
    IAsyncEnumerable<CalendarEvent> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        CancellationToken cancellation = default);
}
