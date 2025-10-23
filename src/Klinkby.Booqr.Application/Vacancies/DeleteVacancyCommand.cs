namespace Klinkby.Booqr.Application.Vacancies;

public sealed partial class DeleteVacancyCommand(
    ICalendarRepository calendar,
    ILogger<DeleteVacancyCommand> logger)
    : Abstractions.DeleteCommand<CalendarEvent>(calendar, logger)
{
    private readonly LoggerMessages _log = new(logger);

    async internal override Task<bool> Delete(AuthenticatedByIdRequest query, CancellationToken cancellation)
    {
        CalendarEvent? vacancy = await calendar.GetById(query.Id, cancellation);

        var bookingConflict = vacancy?.BookingId;
        if (bookingConflict.HasValue)
        {
            _log.CannotDeleteVacancyWithBookingInIt(query.AuthenticatedUserId, bookingConflict.Value);
            throw new InvalidOperationException("There is already a booking within requested time");
        }

        return await base.Delete(query, cancellation);
    }


    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(160, LogLevel.Warning,
            "User {UserId} cannot delete vacancy {Id} because it has a booking")]
        public partial void CannotDeleteVacancyWithBookingInIt(int userId, int id);
    }
}
