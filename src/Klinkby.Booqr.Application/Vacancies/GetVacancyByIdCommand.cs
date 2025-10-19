namespace Klinkby.Booqr.Application.Vacancies;

public sealed class GetVacancyByIdCommand(ICalendarRepository calendar)
    : GetByIdCommand<CalendarEvent>(calendar);
