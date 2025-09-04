namespace Klinkby.Booqr.Application.Calendar;

public sealed class GetVacancyByIdCommand(ICalendarRepository calendar) : GetByIdCommand<CalendarEvent>(calendar);
