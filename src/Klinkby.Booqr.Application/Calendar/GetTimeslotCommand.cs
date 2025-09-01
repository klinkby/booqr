namespace Klinkby.Booqr.Application.Calendar;

public sealed class GetEventCommand(ICalendarRepository events) : ICommand<ByIdRequest, Task<CalendarEvent?>>
{
    public async Task<CalendarEvent?> Execute(ByIdRequest query, CancellationToken cancellation = default)
    {
        return await events
            .GetById(query.Id, cancellation);
    }
}
