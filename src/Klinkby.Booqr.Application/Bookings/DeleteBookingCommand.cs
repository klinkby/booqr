using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using Klinkby.Booqr.Application.Vacancies;

namespace Klinkby.Booqr.Application.Bookings;

public sealed partial class DeleteBookingCommand(
    IBookingRepository bookings,
    ICalendarRepository calendar,
    ITransaction transaction,
    ILogger<DeleteBookingCommand> logger,
    ILogger<AddVacancyCommand> addVacancyLogger)
    : DeleteCommand<Booking>(bookings, logger)
{
    async internal override Task<bool> Delete(AuthenticatedByIdRequest query, CancellationToken cancellation)
    {
        int userId = query.AuthenticatedUserId;
        bool isEmployee = query.User!.IsInRole(UserRole.Employee) || query.User.IsInRole(UserRole.Admin);
        bool deleted;
        CalendarEvent? calendarEvent;

        await transaction.Begin(IsolationLevel.RepeatableRead, cancellation);
        try
        {
            Booking? booking = await bookings.GetById(query.Id, cancellation);
            if (booking is null) return false; // already gone

            var bookingCustomerId = booking.CustomerId;
            if (!isEmployee || bookingCustomerId != userId)
            {
                LogCannotDeleteBooking(logger, query.AuthenticatedUserId, booking.Id);
                throw new UnauthorizedAccessException("You do not have access to delete this booking");
            }

            calendarEvent = await calendar.GetByBookingId(query.Id, cancellation);
            Debug.Assert(calendarEvent is not null);

            await calendar.Delete(calendarEvent.Id, cancellation);
            deleted = await base.Delete(query, cancellation);
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }

        await transaction.Commit(cancellation);

        // reopen the vacancy, joining any adjacent vacancies
        AddVacancyCommand addVacancyCommand = new(calendar, transaction, addVacancyLogger);
        await addVacancyCommand.Execute(Map(calendarEvent, query.User), cancellation);

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

    [LoggerMessage(LogLevel.Warning,
        "User {UserId} is not permitted to delete booking {Id}")]
    private static partial void LogCannotDeleteBooking(ILogger logger, int userId, int id);

}
