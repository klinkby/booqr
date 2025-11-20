using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Commands.Bookings;

public record AddBookingRequest(
    [property: Range(1, int.MaxValue)] int? CustomerId,
    [property: Required]
    [property: Range(1, int.MaxValue)]
    int VacancyId,
    [property: Required]
    [property: Range(1, int.MaxValue)]
    int ServiceId,
    [property: StringLength(8000)]
    string? Notes,
    [property: Required] DateTime StartTime) : AuthenticatedRequest, IEvent
{
    [JsonIgnore]
    public DateTime EndTime { get; internal init; }
}

public partial class AddBookingCommand(
    IBookingRepository bookings,
    ICalendarRepository calendar,
    IServiceRepository services,
    ITransaction transaction,
    IActivityRecorder activityRecorder,
    ILogger<AddBookingCommand> logger)
    : ICommand<AddBookingRequest, Task<int>>
{
    private readonly LoggerMessages _log = new(logger);

    [SuppressMessage("Exceptions usages", "EX006:Do not write logic driven by exceptions.", Justification = "Conflict is an exceptional case")]
    public async Task<int> Execute(AddBookingRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        int userId = query.AuthenticatedUserId;
        int newId;

        if (!query.CustomerId.HasValue)
        {
            query = query with { CustomerId = userId };
        }

        await transaction.Begin(cancellation);
        try
        {
            Service service = await GetAndValidateService(query, userId, cancellation);

            query = query with { EndTime = query.StartTime + service.Duration };

            CalendarEvent vacancy = await GetAndValidateVacancy(query, userId, cancellation);

            if (vacancy.BookingId.HasValue)
            {
                Booking? booking = await bookings.GetById(vacancy.BookingId.Value, cancellation);
                Debug.Assert(booking != null);
                if (booking.CustomerId == userId && booking.ServiceId == query.ServiceId)
                {
                    // this event is already booked by the customer
                    await transaction.Rollback(cancellation);
                    return vacancy.BookingId.Value;
                }
                _log.BookingConflict(userId, vacancy.BookingId.Value);
                throw new InvalidOperationException("The requested vacancy was already booked.");
            }

            newId = await bookings.Add(Map(query), cancellation);
            Covers strategy = GetCoverage(vacancy, query);

            _log.BookingStrategy(userId, newId, strategy);
            Task updateStrategy = strategy switch
            {
                Covers.EntireSlot => UpdateVacancyCoversEntireSlot(vacancy, newId, cancellation),
                Covers.OnlyBeginning => UpdateVacancyCoversOnlyBeginning(vacancy, newId, query, cancellation),
                Covers.OnlyEnd => UpdateVacancyCoversOnlyEnd(vacancy, query, newId, cancellation),
                Covers.SomewhereInTheMiddle => UpdateVacancyInTheMiddle(vacancy, newId, query, cancellation),
                _ => throw new InvalidEnumArgumentException("Unexpected Covers value")
            };
            await updateStrategy;
            activityRecorder.Add<Booking>(new(query.AuthenticatedUserId, newId));
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }
        await transaction.Commit(cancellation);
        _log.CreateBooking(userId, nameof(Booking), newId);
        return newId;
    }

    async private Task<CalendarEvent> GetAndValidateVacancy(AddBookingRequest query, int userId, CancellationToken cancellation)
    {
        CalendarEvent? vacancy = await calendar.GetById(query.VacancyId, cancellation);
        if (vacancy is not null && query.CompletelyWithin(vacancy))
        {
            return vacancy;
        }

        _log.BookingMissingItem(userId, nameof(CalendarEvent), query.VacancyId);
        throw new ArgumentException("The requested vacancy was not found.", nameof(query));
    }

    async private Task<Service> GetAndValidateService(AddBookingRequest query, int userId, CancellationToken cancellation)
    {
        Service? service = await services.GetById(query.ServiceId, cancellation);
        if (service is not null)
        {
            return service;
        }

        _log.BookingMissingItem(userId, nameof(Service), query.VacancyId);
        throw new ArgumentException("The requested service was not found.", nameof(query));
    }

    internal static Covers GetCoverage(CalendarEvent vacancy, AddBookingRequest query)
    {
        if (vacancy.CompletelyWithin(query)) // entire slot is used
        {
            return Covers.EntireSlot;
        }
        if (vacancy.StartTime.Equalsish(query.StartTime)) // only the beginning of the slot is covered
        {
            return Covers.OnlyBeginning;
        }
        if (vacancy.EndTime.Equalsish(query.EndTime)) // only the end of the slot is covered
        {
            return Covers.OnlyEnd;
        }
        // within a larger slot leaving space both in beginning and end
        return Covers.SomewhereInTheMiddle;
    }

    async private Task UpdateVacancyCoversEntireSlot(CalendarEvent vacancy, int newBookingId,
        CancellationToken cancellation)
    {
        vacancy = vacancy with { BookingId = newBookingId };
        await calendar.Update(vacancy, cancellation);
    }

    async private Task UpdateVacancyCoversOnlyBeginning(CalendarEvent vacancy, int newBookingId, AddBookingRequest query,
        CancellationToken cancellation)
    {
        await calendar.Update(vacancy with { BookingId = newBookingId, EndTime = query.EndTime}, cancellation);
        await calendar.Add(vacancy with { StartTime = query.EndTime }, cancellation);
    }

    async private Task UpdateVacancyCoversOnlyEnd(CalendarEvent vacancy, AddBookingRequest query, int newBookingId,
        CancellationToken cancellation)
    {
        await calendar.Add(vacancy with { EndTime = query.StartTime }, cancellation);
        await calendar.Update(vacancy with { BookingId = newBookingId, StartTime = query.StartTime}, cancellation);
    }

    async private Task UpdateVacancyInTheMiddle(CalendarEvent vacancy, int newBookingId, AddBookingRequest query,
        CancellationToken cancellation)
    {
        await calendar.Add(vacancy with { StartTime = query.EndTime }, cancellation);
        await calendar.Add(vacancy with { EndTime = query.StartTime }, cancellation);
        await calendar.Update(vacancy with { BookingId = newBookingId, StartTime = query.StartTime, EndTime = query.EndTime }, cancellation);
    }

    private static Booking Map(AddBookingRequest query) =>
        new(query.CustomerId!.Value, query.ServiceId, query.Notes);

    private sealed partial class LoggerMessages(ILogger logger)
    {
        private readonly ILogger _logger = logger;

        [LoggerMessage(100, LogLevel.Information, "User {UserId} created {Type}:{Id}")]
        public partial void CreateBooking(int userId, string type, int id);

        [LoggerMessage(101, LogLevel.Warning, "User {UserId} tried to get missing {Type} {Id}")]
        public partial void BookingMissingItem(int userId, string type, int id);

        [LoggerMessage(102, LogLevel.Warning, "User {UserId} tried to book already booked {Id}")]
        public partial void BookingConflict(int userId, int id);

        [LoggerMessage(103, LogLevel.Warning, "User {UserId} booking {Id} use vacancy strategy {Covers}")]
        public partial void BookingStrategy(int userId, int id, Covers covers);
    }
}

public enum Covers
{
    EntireSlot,
    OnlyBeginning,
    OnlyEnd,
    SomewhereInTheMiddle
}
