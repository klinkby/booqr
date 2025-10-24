namespace Klinkby.Booqr.Application.Commands.Vacancies;

public sealed class GetVacancyByIdCommand(ICalendarRepository calendar)
    : GetByIdCommand<CalendarEvent>(calendar);
