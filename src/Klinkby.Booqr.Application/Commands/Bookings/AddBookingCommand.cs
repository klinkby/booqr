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
    : ICommand<AddBookingRequest, Task<Result<int>>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<int>> Execute(AddBookingRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        int userId = query.AuthenticatedUserId;

        if (query.CustomerId is { } customerId && !query.IsOwnerOrEmployee(customerId))
        {
            _log.CannotBookForOther(userId, customerId);
            return Problem.Forbidden with { Detail = "You cannot create a booking for another customer." };
        }
        query = query with { CustomerId = query.CustomerId ?? userId };

        await transaction.Begin(cancellation);
        Result<int> result;
        bool commit;
        try
        {
            (result, commit) = await CreateBooking(query, userId, cancellation);
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }

        if (commit)
        {
            await transaction.Commit(cancellation);
            _log.CreateBooking(userId, nameof(Booking), ((Result<int>.Success)result).Value);
        }
        else
        {
            await transaction.Rollback(cancellation);
        }

        return result;
    }

    private async Task<(Result<int> Result, bool Commit)> CreateBooking(AddBookingRequest query, int userId, CancellationToken cancellation)
    {
        Service? service = await GetAndValidateService(query, userId, cancellation);
        if (service is null)
        {
            return (Problem.NotFound with { Detail = "The requested service was not found." }, false);
        }

        query = query with { EndTime = query.StartTime + service.Duration };

        CalendarEvent? vacancy = await GetAndValidateVacancy(query, userId, cancellation);
        if (vacancy is null)
        {
            return (Problem.NotFound with { Detail = "The requested vacancy was not found." }, false);
        }

        if (vacancy.BookingId.HasValue)
        {
            Booking? booking = await bookings.GetById(vacancy.BookingId.Value, cancellation);
            Debug.Assert(booking != null);
            return booking.CustomerId == userId && booking.ServiceId == query.ServiceId
                ? (vacancy.BookingId.Value, false) // already booked by this customer, nothing to commit
                : (GetConflict(userId, vacancy.BookingId.Value), false);
        }

        var newId = await bookings.Add(Map(query), cancellation);
        Covers strategy = GetCoverage(vacancy, query);
        _log.BookingStrategy(userId, newId, strategy);

        Task updateStrategy = strategy switch
        {
            Covers.EntireSlot => UpdateVacancyCoversEntireSlot(vacancy, newId, cancellation),
            Covers.OnlyBeginning => UpdateVacancyCoversOnlyBeginning(vacancy, newId, query, cancellation),
            Covers.OnlyEnd => UpdateVacancyCoversOnlyEnd(vacancy, query, newId, cancellation),
            Covers.SomewhereInTheMiddle => UpdateVacancyInTheMiddle(vacancy, newId, query, cancellation),
            _ => throw new UnreachableException("Covers enum has no more values.")
        };
        await updateStrategy;
        activityRecorder.Add<Booking>(new(query.AuthenticatedUserId, newId));

        return (newId, true);
    }

    private Problem GetConflict(int userId, int vacancyBookingId)
    {
        _log.BookingConflict(userId, vacancyBookingId);
        return Problem.Conflict with { Detail = "The requested vacancy was already booked." };
    }

    private async Task<CalendarEvent?> GetAndValidateVacancy(AddBookingRequest query, int userId, CancellationToken cancellation)
    {
        CalendarEvent? vacancy = await calendar.GetById(query.VacancyId, cancellation);
        if (vacancy is not null && query.CompletelyWithin(vacancy))
        {
            return vacancy;
        }

        _log.BookingMissingItem(userId, nameof(CalendarEvent), query.VacancyId);
        return null;
    }

    private async Task<Service?> GetAndValidateService(AddBookingRequest query, int userId, CancellationToken cancellation)
    {
        Service? service = await services.GetById(query.ServiceId, cancellation);
        if (service is not null)
        {
            return service;
        }

        _log.BookingMissingItem(userId, nameof(Service), query.VacancyId);
        return null;
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

    private async Task UpdateVacancyCoversEntireSlot(CalendarEvent vacancy, int newBookingId,
        CancellationToken cancellation)
    {
        vacancy = vacancy with { BookingId = newBookingId };
        await calendar.Update(vacancy, cancellation);
    }

    private async Task UpdateVacancyCoversOnlyBeginning(CalendarEvent vacancy, int newBookingId, AddBookingRequest query,
        CancellationToken cancellation)
    {
        await calendar.Update(vacancy with { BookingId = newBookingId, EndTime = query.EndTime}, cancellation);
        await calendar.Add(vacancy with { StartTime = query.EndTime }, cancellation);
    }

    private async Task UpdateVacancyCoversOnlyEnd(CalendarEvent vacancy, AddBookingRequest query, int newBookingId,
        CancellationToken cancellation)
    {
        await calendar.Update(vacancy with { BookingId = newBookingId, StartTime = query.StartTime}, cancellation);
        await calendar.Add(vacancy with { EndTime = query.StartTime }, cancellation);
    }

    private async Task UpdateVacancyInTheMiddle(CalendarEvent vacancy, int newBookingId, AddBookingRequest query,
        CancellationToken cancellation)
    {
        await calendar.Update(vacancy with { BookingId = newBookingId, StartTime = query.StartTime, EndTime = query.EndTime }, cancellation);
        await calendar.Add(vacancy with { StartTime = query.EndTime }, cancellation);
        await calendar.Add(vacancy with { EndTime = query.StartTime }, cancellation);
    }

    private static Booking Map(AddBookingRequest query) =>
        new(query.CustomerId!.Value, query.ServiceId, query.Notes);

    [ExcludeFromCodeCoverage]
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

        [LoggerMessage(104, LogLevel.Warning, "User {UserId} cannot create booking for customer {CustomerId}")]
        public partial void CannotBookForOther(int userId, int customerId);
    }
}

public enum Covers
{
    EntireSlot,
    OnlyBeginning,
    OnlyEnd,
    SomewhereInTheMiddle
}
