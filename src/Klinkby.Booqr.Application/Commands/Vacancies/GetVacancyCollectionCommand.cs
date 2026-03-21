namespace Klinkby.Booqr.Application.Commands.Vacancies;

public sealed record GetVacanciesRequest(
    DateTime? FromTime,
    DateTime? ToTime,
    int? PageStart,
    int? PageNum) : PageQuery(PageStart, PageNum);

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
