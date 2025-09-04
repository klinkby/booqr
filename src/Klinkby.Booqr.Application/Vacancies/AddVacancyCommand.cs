using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Klinkby.Booqr.Application.Extensions;

namespace Klinkby.Booqr.Application.Vacancies;

public sealed record AddVacancyRequest(
    [property: Range(1, int.MaxValue)]
    int? EmployeeId,
    [property: Required]
    [property: Range(1, int.MaxValue)]
    int LocationId,
    [property: Required] DateTime StartTime,
    [property: Required] DateTime EndTime) : AuthenticatedRequest, IEvent
{
    /// <summary>
    ///     determine if it overlaps the start of existing, so we can just prepend that to the new start time
    /// </summary>
    internal bool TryExtendStart(CalendarEvent? intersectsStartOf,
        [NotNullWhen(true)] out CalendarEvent? extend)
    {
        if (intersectsStartOf is not null)
        {
            extend = intersectsStartOf with { StartTime = StartTime };
            return true;
        }

        extend = null;
        return false;
    }

    /// <summary>
    ///     determine if it overlaps the end of existing, so we can just extend that to the new end time
    /// </summary>
    internal bool TryExtendEnd(CalendarEvent? intersectsEndOf,
        [NotNullWhen(true)] out CalendarEvent? extend)
    {
        if (intersectsEndOf is not null)
        {
            extend = intersectsEndOf with { EndTime = EndTime };
            return true;
        }

        extend = null;
        return false;
    }

    /// <summary>
    ///     any of those are completely overlapped by the request can be deleted
    /// </summary>
    internal bool TryGetCompletelyOverlapped(
        IReadOnlyList<CalendarEvent> events,
        [NotNullWhen(true)] out IReadOnlyList<CalendarEvent>? obsoleteCollection)
    {
        var list = events
            .Where(e => e.CompletelyWithin(this))
            .ToList();
        obsoleteCollection = list.Count == 0 ? null : list;
        return list.Count != 0;
    }

    /// <summary>
    ///     find any events that end overlaps with the new one
    ///     and start overlaps with the new one
    /// </summary>
    internal (CalendarEvent? EndOf, CalendarEvent? StartOf) FindIntersecting(
        IReadOnlyList<CalendarEvent> events)
    {
        (List<CalendarEvent> EndOf, List<CalendarEvent> StartOf) intersects =
            (events
                    .Where(e => this.StartIntersects(e))
                    .ToList(),
                events
                    .Where(e => e.StartIntersects(this))
                    .ToList());

        Debug.Assert(intersects.EndOf.Count <= 1);
        Debug.Assert(intersects.StartOf.Count <= 1);
        Debug.Assert(intersects.StartOf.Count + intersects.StartOf.Count <= 2);
        return (intersects.EndOf.FirstOrDefault(), intersects.StartOf.FirstOrDefault());
    }

    /// <summary>
    ///     determine if the requested timespan is completely within an existing vacancy
    /// </summary>
    internal bool TryGetCompletelyCovered(IReadOnlyList<CalendarEvent> events,
        [NotNullWhen(true)] out CalendarEvent? completelyWithin)
    {
        completelyWithin = events.FirstOrDefault(e => this.CompletelyWithin(e) && !e.BookingId.HasValue);
        return completelyWithin is not null;
    }

    /// <summary>
    ///     determine if conflicting events are on another location
    /// </summary>
    internal bool TryGetEventWithConflictingLocation(IReadOnlyList<CalendarEvent> events,
        [NotNullWhen(true)] out CalendarEvent? locationConflict)
    {
        locationConflict = events.FirstOrDefault(e =>
            e.LocationId != LocationId && this.Contains(e));
        return locationConflict is not null;
    }
}

public sealed partial class AddVacancyCommand(ICalendarRepository calendar, ITransaction transaction, ILogger<AddVacancyCommand> logger)
    : ICommand<AddVacancyRequest, Task<int>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<int> Execute(AddVacancyRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var userId = query.AuthenticatedUserId;

        if (!query.EmployeeId.HasValue)
        {
            query = query with { EmployeeId = userId };
        }

        int newId;
        await transaction.Begin(IsolationLevel.RepeatableRead, cancellation);
        try
        {
            List<CalendarEvent> events = await calendar
                .GetRange(query.StartTime, query.EndTime, new PageQuery(), true, true, cancellation)
                .Where(e => e.EmployeeId == userId)
                .ToListAsync(cancellation);

            newId = await AddCalendarEvent(query, events, userId, cancellation);
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }
        await transaction.Commit(cancellation);
        return newId;
    }

    async internal Task<int> AddCalendarEvent(AddVacancyRequest query, List<CalendarEvent> events, int userId,
        CancellationToken cancellation)
    {
        if (TryGetEventWithBooking(events, out CalendarEvent? bookingConflict))
        {
            _log.LogCannotAddVacancyWithBookingInIt(userId, query.StartTime, query.EndTime, bookingConflict.Id);
            throw new InvalidOperationException("There is already a booking within requested time");
        }

        if (query.TryGetEventWithConflictingLocation(events, out CalendarEvent? locationConflict))
        {
            _log.LogCannotAddVacancyWithConflictingLocation(userId, query.StartTime, query.EndTime,
                locationConflict.Id);
            throw new InvalidOperationException("There is already a vacancy at a different location");
        }

        // now consider only events from this location
        events = events.Where(e => e.LocationId == query.LocationId).ToList();

        if (query.TryGetCompletelyCovered(events, out CalendarEvent? completelyWithin))
        {
            _log.LogUserCreateTypeWithinId(userId, completelyWithin.Id);
            return completelyWithin.Id;
        }

        if (query.TryGetCompletelyOverlapped(events, out IReadOnlyList<CalendarEvent>? overlapped))
        {
            foreach (CalendarEvent obsolete in overlapped)
            {
                _log.LogRemoveOverlapped(userId, query.StartTime, query.EndTime, obsolete.Id);
                await calendar.Delete(obsolete.Id, cancellation);
                events.Remove(obsolete);
            }
        }

        (CalendarEvent? EndOf, CalendarEvent? StartOf) intersects = query.FindIntersecting(events);

        if (TryCombineIntersectingEvents(intersects, out var obsoleteId, out CalendarEvent? combined))
        {
            _log.LogRemoveOverlapped(userId, query.StartTime, query.EndTime, obsoleteId);
            await calendar.Delete(obsoleteId, cancellation);

            _log.LogExtendIntersecting(userId, query.StartTime, query.EndTime, combined.Id);
            await calendar.Update(combined, cancellation);

            return combined.Id;
        }

        if (query.TryExtendEnd(intersects.EndOf, out CalendarEvent? extendedEnd))
        {
            _log.LogExtendIntersecting(userId, query.StartTime, query.EndTime, extendedEnd.Id);
            await calendar.Update(extendedEnd, cancellation);
            return extendedEnd.Id;
        }

        if (query.TryExtendStart(intersects.StartOf, out CalendarEvent? extendedStart))
        {
            _log.LogExtendIntersecting(userId, query.StartTime, query.EndTime, extendedStart.Id);
            await calendar.Update(extendedStart, cancellation);
            return extendedStart.Id;
        }

        var newId = await AddNewVacancy(query, cancellation);
        _log.LogUserCreateTypeId(userId, "Vacancy", newId);
        return newId;
    }

    /// <summary>
    ///     no intersections simply add the new vacancy
    /// </summary>
    async private Task<int> AddNewVacancy(AddVacancyRequest query, CancellationToken cancellation)
    {
        var newId = await calendar.Add(Map(query), cancellation);
        return newId;
    }

    /// <summary>
    ///     ensure none of the events has bookings
    /// </summary>
    internal static bool TryGetEventWithBooking(IReadOnlyList<CalendarEvent> events,
        [NotNullWhen(true)] out CalendarEvent? bookingConflict)
    {
        bookingConflict = events.FirstOrDefault(x => x.BookingId.HasValue);
        return bookingConflict is not null;
    }

    /// <summary>
    ///     combine two existing slots
    /// </summary>
    internal static bool TryCombineIntersectingEvents(
        (CalendarEvent? EndOf, CalendarEvent? StartOf) intersects,
        out int obsoleteId, [NotNullWhen(true)] out CalendarEvent? extend)
    {
        if (intersects.StartOf is not null && intersects.EndOf is not null)
        {
            Debug.Assert(intersects.StartOf.Id != intersects.EndOf.Id);

            CalendarEvent obsolete = intersects.StartOf;
            obsoleteId = obsolete.Id;

            extend = intersects.EndOf with { EndTime = obsolete.EndTime };
            return true;
        }

        obsoleteId = 0;
        extend = null;
        return false;
    }

    private static CalendarEvent Map(AddVacancyRequest query)
    {
        return new CalendarEvent(
            query.AuthenticatedUserId,
            query.LocationId,
            null,
            query.StartTime,
            query.EndTime);
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        private readonly ILogger _logger = logger;

        [LoggerMessage(LogLevel.Information, "User {UserId} created {Type}:{Id}")]
        public partial void LogUserCreateTypeId(int userId, string type, int id);

        [LoggerMessage(LogLevel.Information, "User {UserId} created vacancy but is within {Id}")]
        public partial void LogUserCreateTypeWithinId(int userId, int id);

        [LoggerMessage(LogLevel.Warning,
            "User {UserId} cannot create vacancy in {StartTime} - {EndTime} because {Id} has a booking")]
        public partial void
            LogCannotAddVacancyWithBookingInIt(int userId, DateTime startTime, DateTime endTime, int id);

        [LoggerMessage(LogLevel.Warning,
            "User {UserId} cannot create vacancy in {StartTime} - {EndTime} because {Id} has a different location")]
        public partial void LogCannotAddVacancyWithConflictingLocation(int userId, DateTime startTime, DateTime endTime,
            int id);

        [LoggerMessage(LogLevel.Information,
            "User {UserId} will create new vacancy in {StartTime} - {EndTime} obsoletes {Id}")]
        public partial void LogRemoveOverlapped(int userId, DateTime startTime, DateTime endTime, int id);

        [LoggerMessage(LogLevel.Information,
            "User {UserId} create new vacancy in {StartTime} - {EndTime} extends {Id}")]
        public partial void LogExtendIntersecting(int userId, DateTime startTime, DateTime endTime, int id);
    }
}
