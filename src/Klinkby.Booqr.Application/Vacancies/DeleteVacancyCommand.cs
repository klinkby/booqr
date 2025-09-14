namespace Klinkby.Booqr.Application.Vacancies;

public sealed partial class DeleteVacancyCommand(
    ICalendarRepository calendar,
    ILogger<DeleteVacancyCommand> logger)
    : DeleteCommand<CalendarEvent>(calendar, logger)
{
    async internal override Task<bool> Delete(AuthenticatedByIdRequest query, CancellationToken cancellation)
    {
        CalendarEvent? vacancy = await calendar.GetById(query.Id, cancellation);

        var bookingConflict = vacancy?.BookingId;
        if (bookingConflict.HasValue)
        {
            LogCannotDeleteVacancyWithBookingInIt(logger, query.AuthenticatedUserId, bookingConflict.Value);
            throw new InvalidOperationException("There is already a booking within requested time");
        }

        return await base.Delete(query, cancellation);
    }

    [LoggerMessage(160, LogLevel.Warning,
        "User {UserId} cannot delete vacancy {Id} because it has a booking")]
    private static partial void LogCannotDeleteVacancyWithBookingInIt(ILogger logger, int userId, int id);

}
