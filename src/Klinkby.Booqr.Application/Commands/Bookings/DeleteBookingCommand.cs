using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Klinkby.Booqr.Application.Commands.Vacancies;

namespace Klinkby.Booqr.Application.Commands.Bookings;

public sealed partial class DeleteBookingCommand(
    IBookingRepository bookings,
    ICalendarRepository calendar,
    ITransaction transaction,
    IActivityRecorder activityRecorder,
    ILogger<DeleteBookingCommand> logger,
    ILogger<AddVacancyCommand> addVacancyLogger)
    : DeleteCommand<Booking>(bookings, activityRecorder, logger)
{
    private readonly LoggerMessages _log = new(logger);
    private readonly IActivityRecorder _activityRecorder = activityRecorder;

    [SuppressMessage("Exceptions usages", "EX006:Do not write logic driven by exceptions.", Justification = "Unauthorized is an exceptional case")]
    async internal override Task<bool> Delete(AuthenticatedByIdRequest query, CancellationToken cancellation)
    {
        bool deleted;

        await transaction.Begin(IsolationLevel.RepeatableRead, cancellation);
        try
        {
            Booking? booking = await bookings.GetById(query.Id, cancellation);
            if (booking is null) return true; // idempotence, already gone

            if (!query.IsOwnerOrEmployee(booking.CustomerId))
            {
                _log.CannotDeleteBooking(query.AuthenticatedUserId, booking.Id);
                throw new UnauthorizedAccessException("You do not have access to delete this booking");
            }

            CalendarEvent? calendarEvent = await calendar.GetByBookingId(query.Id, cancellation);
            Debug.Assert(calendarEvent is not null);

            await calendar.Delete(calendarEvent.Id, cancellation);
            deleted = await base.Delete(query, cancellation);

            // reopen the vacancy, joining any adjacent vacancies
            AddVacancyCommand addVacancyCommand = new(calendar, transaction, _activityRecorder, addVacancyLogger);
            await addVacancyCommand.AddVacancyCore(Map(calendarEvent, query.User), calendarEvent.EmployeeId, cancellation);
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }

        await transaction.Commit(cancellation);

        return deleted;
    }

    private static AddVacancyRequest Map(CalendarEvent calendarEvent, ClaimsPrincipal user) =>
        new(calendarEvent.EmployeeId,
            calendarEvent.LocationId,
            calendarEvent.StartTime,
            calendarEvent.EndTime)
        {
            User = user
        };


    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(110, LogLevel.Warning, "User {UserId} is not permitted to delete booking {Id}")]
        public partial void CannotDeleteBooking(int userId, int id);
    }
}
