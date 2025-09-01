namespace Klinkby.Booqr.Application.Calendar;

public sealed record GetAvailableEventsRequest(
    DateTime? FromTime,
    DateTime? ToTime,
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100)
    : IPageQuery;

public sealed class GetAvailableEventsCommand(ICalendarRepository events)
    : ICommand<GetAvailableEventsRequest, IAsyncEnumerable<CalendarEvent>>
{
    public IAsyncEnumerable<CalendarEvent> Execute(GetAvailableEventsRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return events.GetRange(
                query.FromTime ?? DateTime.MinValue,
                query.ToTime ?? DateTime.MaxValue,
                query,
                cancellation);
    }
}
