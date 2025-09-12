namespace Klinkby.Booqr.Application.Calendar;

public sealed record GetVacanciesRequest(
    DateTime? FromTime,
    DateTime? ToTime,
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100)
    : IPageQuery;

public sealed class GetVacancyCollectionCommand(ICalendarRepository events, TimeProvider timeProvider)
    : ICommand<GetVacanciesRequest, IAsyncEnumerable<CalendarEvent>>
{
    public IAsyncEnumerable<CalendarEvent> Execute(GetVacanciesRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return events.GetRange(
            query.FromTime ?? timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
            query.ToTime ?? DateTime.MaxValue,
            query,
            true,
            false,
            cancellation);
    }
}
