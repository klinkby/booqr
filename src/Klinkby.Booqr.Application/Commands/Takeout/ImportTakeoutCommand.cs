using System.Data;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.Takeout;

/// <summary>
///     Rehydrates a <see cref="TakeoutScope.TenantWorkspace" /> document into the current tenant as an idempotent merge.
/// </summary>
/// <remarks>
///     <para>
///         Security: every foreign key written is taken from an id map whose values are either ids read through the
///         tenant's RLS-scoped connection or ids returned by our own inserts — never a raw document id. This matters
///         because PostgreSQL FK checks bypass RLS, so a raw id could otherwise reference another tenant's row.
///     </para>
///     <para>
///         Existing users are only matched, never modified (no role/profile changes); imported users get no password.
///         Natural-key and calendar-overlap conflicts are detected in memory before any INSERT so a constraint violation
///         does not poison the transaction (25P02).
///     </para>
/// </remarks>
public sealed partial class ImportTakeoutCommand(
    IUserRepository users,
    ILocationRepository locations,
    IServiceRepository services,
    IEmployeeServiceRepository employeeServices,
    IBookingRepository bookings,
    ICalendarRepository calendar,
    ITransaction transaction,
    IActivityRecorder activityRecorder,
    TimeProvider timeProvider,
    ILogger<ImportTakeoutCommand> logger)
    : ICommand<ImportTakeoutRequest, Task<Result<ImportTakeoutResponse>>>
{
    /// <summary>Upper bound on the number of entities in one document (DoS guard; one transaction).</summary>
    internal const int MaxEntities = 50_000;

    private static readonly PageQuery All = TakeoutQueries.All;

    private static readonly Problem NotAdmin =
        Problem.Forbidden with { Detail = "Only administrators can import a takeout document." };

    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<ImportTakeoutResponse>> Execute(ImportTakeoutRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var userId = query.AuthenticatedUserId;

        if (!query.User.IsInRole(UserRole.Admin))
        {
            _log.Forbidden(userId);
            return NotAdmin;
        }

        TakeoutDocument? document = query.Document;
        if (document is null)
        {
            return Problem.ValidationFailed with { Detail = "A takeout document is required." };
        }

        if (document.Scope != TakeoutScope.TenantWorkspace)
        {
            _log.WrongScope(userId, document.Scope);
            return Problem.ValidationFailed with
            {
                Detail = $"Only {nameof(TakeoutScope.TenantWorkspace)} documents can be imported."
            };
        }

        var source = new Source(document);
        if (source.Count > MaxEntities)
        {
            return Problem.ValidationFailed with
            {
                Detail = $"The document contains more than {MaxEntities} entities."
            };
        }

        var run = new ImportRun(timeProvider.GetUtcNow().UtcDateTime);
        if (document.SchemaVersion != TakeoutDocument.CurrentSchemaVersion)
        {
            run.Warn(TakeoutWarningCode.SchemaVersionMismatch, "Document", null,
                $"Document schema version {document.SchemaVersion} differs from supported version {TakeoutDocument.CurrentSchemaVersion}.");
        }

        await transaction.Begin(IsolationLevel.RepeatableRead, cancellation);
        bool isAdmin;
        try
        {
            // ASVS 8.3.2: the JWT role claim may be up to one access-token lifetime stale, so re-authorize
            // against the database. Like ChangeUserRoleCommand, lock the admin rows so the caller cannot be
            // demoted while this import (which may grant roles) runs.
            isAdmin = (await users.LockAdmins(cancellation)).Contains(userId);
            if (isAdmin)
            {
                await ImportUsers(source.Users, run, cancellation);
                await ImportLocations(source.Locations, run, cancellation);
                await ImportServices(source.Services, run, cancellation);
                await ImportCalendar(source.Calendar, source.Bookings, run, cancellation);
            }
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }

        if (!isAdmin)
        {
            await transaction.Rollback(cancellation);
            _log.NotAdminInDatabase(userId);
            return NotAdmin;
        }

        if (query.ValidateOnly)
        {
            await transaction.Rollback(cancellation);
        }
        else
        {
            await transaction.Commit(cancellation);
            await RecordActivities(query, run, cancellation);
        }

        _log.Imported(userId, query.ValidateOnly, run.Warnings.Count);
        return run.ToResponse(query.ValidateOnly);
    }

    /// <summary>
    ///     Audit trail after commit (ASVS 16.3.3): the import itself, plus one <c>ChangeRole</c> entry per
    ///     privileged account it created — the same audit record <see cref="Klinkby.Booqr.Application.Commands.Users.ChangeUserRoleCommand" /> writes,
    ///     so role grants via import are not invisible.
    /// </summary>
    private async Task RecordActivities(ImportTakeoutRequest query, ImportRun run, CancellationToken cancellation)
    {
        var userId = query.AuthenticatedUserId;
        await activityRecorder.Import<TakeoutDocument>(new(userId, 0, query.TenantId), cancellation);
        foreach (var privilegedId in run.PrivilegedUsersCreated)
        {
            await activityRecorder.ChangeRole<User>(new(userId, privilegedId, query.TenantId), cancellation);
        }
    }

    private async Task ImportUsers(IReadOnlyList<TakeoutUser> source, ImportRun run, CancellationToken cancellation)
    {
        const string entity = nameof(User);
        Dictionary<string, User> byEmail = new(StringComparer.OrdinalIgnoreCase);
        await foreach (User user in users.GetAll(All, cancellation))
        {
            byEmail.TryAdd(user.Email, user);
        }

        foreach (TakeoutUser item in source)
        {
            if (run.UserMap.ContainsKey(item.Id))
            {
                run.SkipDuplicate(entity, item.Id, run.UserCounts);
                continue;
            }

            var email = NormalizeEmail(item.Email);
            if (email is null)
            {
                run.Warn(TakeoutWarningCode.UnmatchableUserSkipped, entity, item.Id,
                    "User has no email, so it can be neither matched nor created.");
                run.UserCounts.Skipped++;
                continue;
            }

            if (byEmail.TryGetValue(email, out User? existing))
            {
                run.MapUser(item.Id, existing.Id, existing.Role);
                run.Matched(entity, item.Id, run.UserCounts,
                    "Matched an existing user by email; the existing user was left unchanged.");
                continue;
            }

            if (!IsKnownRole(item.Role))
            {
                run.Warn(TakeoutWarningCode.MissingReferenceSkipped, entity, item.Id, "User has an unknown role.");
                run.UserCounts.Skipped++;
                continue;
            }

            var created = new User(email, null, item.Role, TrimToNull(item.Name), item.Phone);
            var newId = await users.Add(created, cancellation);
            byEmail[email] = created with { Id = newId };
            run.MapUser(item.Id, newId, item.Role);
            run.UserCounts.Created++;
            run.Warn(TakeoutWarningCode.UserCreatedWithoutPassword, entity, item.Id,
                "User was created without a password and must use password reset to sign in.");
            if (item.Role is UserRole.Admin or UserRole.Employee)
            {
                // ASVS 8.2.3: the document's role field grants privileges; surface every grant explicitly.
                run.PrivilegedUsersCreated.Add(newId);
                run.Warn(TakeoutWarningCode.PrivilegedUserCreated, entity, item.Id,
                    $"User was created with the privileged role {item.Role}.");
            }
        }
    }

    private async Task ImportLocations(IReadOnlyList<TakeoutLocation> source, ImportRun run,
        CancellationToken cancellation)
    {
        const string entity = nameof(Location);
        Dictionary<string, int> byName = new(StringComparer.OrdinalIgnoreCase);
        await foreach (Location location in locations.GetAll(All, cancellation))
        {
            byName.TryAdd(location.Name.Trim(), location.Id);
        }

        foreach (TakeoutLocation item in source)
        {
            if (run.LocationMap.ContainsKey(item.Id))
            {
                run.SkipDuplicate(entity, item.Id, run.LocationCounts);
                continue;
            }

            var name = TrimToNull(item.Name);
            if (name is null)
            {
                run.Warn(TakeoutWarningCode.MissingReferenceSkipped, entity, item.Id, "Location has no name.");
                run.LocationCounts.Skipped++;
                continue;
            }

            if (byName.TryGetValue(name, out var existingId))
            {
                run.LocationMap[item.Id] = existingId;
                run.Matched(entity, item.Id, run.LocationCounts, "Matched an existing location by name.");
                continue;
            }

            var newId = await locations.Add(
                new Location(name, TrimToNull(item.Address1), TrimToNull(item.Address2), TrimToNull(item.Zip),
                    TrimToNull(item.City)), cancellation);
            byName[name] = newId;
            run.LocationMap[item.Id] = newId;
            run.LocationCounts.Created++;
        }
    }

    private async Task ImportServices(IReadOnlyList<TakeoutService> source, ImportRun run,
        CancellationToken cancellation)
    {
        const string entity = nameof(Service);
        Dictionary<string, Service> byName = new(StringComparer.OrdinalIgnoreCase);
        await foreach (Service service in services.GetAll(All, cancellation))
        {
            byName.TryAdd(service.Name.Trim(), service);
        }

        foreach (TakeoutService item in source)
        {
            if (run.ServiceMap.ContainsKey(item.Id))
            {
                run.SkipDuplicate(entity, item.Id, run.ServiceCounts);
                continue;
            }

            var name = TrimToNull(item.Name);
            if (name is null || item.Duration <= TimeSpan.Zero)
            {
                run.Warn(TakeoutWarningCode.InvalidTimeRangeSkipped, entity, item.Id,
                    "Service must have a name and a positive duration.");
                run.ServiceCounts.Skipped++;
                continue;
            }

            int[] employees = MapEmployees(item, run);
            if (byName.TryGetValue(name, out Service? existing))
            {
                run.ServiceMap[item.Id] = existing.Id;
                run.Matched(entity, item.Id, run.ServiceCounts,
                    "Matched an existing service by name; employee assignments were merged.");
                int[] merged = existing.Employees.Union(employees).Order().ToArray();
                if (merged.Length != existing.Employees.Length)
                {
                    // Assign replaces the set, so pass the union to keep existing assignments.
                    await employeeServices.Assign(existing.Id, merged, cancellation);
                    byName[name] = existing with { Employees = merged };
                }

                continue;
            }

            var newId = await services.Add(
                new Service(name, item.Duration, [], TrimToNull(item.Description)), cancellation);
            if (employees.Length > 0)
            {
                await employeeServices.Assign(newId, employees, cancellation);
            }

            byName[name] = new Service(name, item.Duration, employees) { Id = newId };
            run.ServiceMap[item.Id] = newId;
            run.ServiceCounts.Created++;
        }
    }

    private static int[] MapEmployees(TakeoutService item, ImportRun run)
    {
        HashSet<int> mapped = [];
        foreach (var sourceEmployeeId in item.EmployeeIds ?? [])
        {
            if (run.TryMapStaff(sourceEmployeeId, out var employeeId))
            {
                mapped.Add(employeeId);
            }
            else
            {
                run.Warn(TakeoutWarningCode.MissingReferenceSkipped, nameof(Service), item.Id,
                    $"Employee {sourceEmployeeId} was not imported or is not staff; assignment skipped.");
            }
        }

        return mapped.ToArray();
    }

    private async Task ImportCalendar(IReadOnlyList<TakeoutCalendarSlot> source,
        IReadOnlyList<TakeoutBooking> sourceBookings, ImportRun run, CancellationToken cancellation)
    {
        const string entity = nameof(CalendarEvent);
        Dictionary<int, TakeoutBooking> bookingsById = [];
        foreach (TakeoutBooking booking in sourceBookings)
        {
            if (!bookingsById.TryAdd(booking.Id, booking))
            {
                run.SkipDuplicate(nameof(Booking), booking.Id, run.BookingCounts);
            }
        }

        // Only existing events ending after the earliest imported start can collide with an imported slot.
        DateTime earliestStart = source.Count == 0 ? DateTime.MaxValue : source.Min(x => ToUtc(x.StartTime));
        Dictionary<int, List<CalendarEvent>> byEmployee = [];
        await foreach (CalendarEvent existing in calendar.GetAll(All, cancellation))
        {
            if (existing.EndTime > earliestStart)
            {
                EventsOf(byEmployee, existing.EmployeeId).Add(existing);
            }
        }

        HashSet<int> seenSlots = [];
        HashSet<int> usedBookings = [];
        IEnumerable<(TakeoutCalendarSlot Item, TakeoutCalendarSlot Slot)> ordered = source
            .Select(x => (x, x with { StartTime = ToUtc(x.StartTime), EndTime = ToUtc(x.EndTime) }))
            .OrderBy(x => x.Item2.StartTime);
        foreach ((TakeoutCalendarSlot item, TakeoutCalendarSlot slot) in ordered)
        {
            if (!seenSlots.Add(item.Id))
            {
                run.SkipDuplicate(entity, item.Id, run.CalendarCounts);
                continue;
            }

            if (slot.EndTime <= slot.StartTime)
            {
                run.SkipSlot(TakeoutWarningCode.InvalidTimeRangeSkipped, item, "Slot must end after it starts.");
                continue;
            }

            if (slot.EndTime <= run.Now)
            {
                run.SkipSlot(TakeoutWarningCode.HistoricalEntrySkipped, item, "Slot is in the past.");
                continue;
            }

            if (!run.TryMapStaff(slot.EmployeeId, out var employeeId)
                || !run.LocationMap.TryGetValue(slot.LocationId, out var locationId))
            {
                run.SkipSlot(TakeoutWarningCode.MissingReferenceSkipped, item,
                    "Slot references an employee or location that was not imported.");
                continue;
            }

            Booking? booking = null;
            if (slot.BookingId is { } sourceBookingId)
            {
                if (!usedBookings.Add(sourceBookingId))
                {
                    run.SkipSlot(TakeoutWarningCode.DuplicateSourceIdSkipped, item,
                        $"Booking {sourceBookingId} is already used by another slot.");
                    continue;
                }

                booking = MapBooking(sourceBookingId, bookingsById, run);
                if (booking is null)
                {
                    run.SkipSlot(TakeoutWarningCode.MissingReferenceSkipped, item,
                        $"Slot references booking {sourceBookingId} whose customer or service was not imported.",
                        withBooking: true);
                    continue;
                }
            }

            var target = new CalendarEvent(employeeId, locationId, null, slot.StartTime, slot.EndTime);
            List<CalendarEvent> employeeEvents = EventsOf(byEmployee, employeeId);
            (int overlaps, int sameIndex) = FindOverlaps(employeeEvents, target);

            if (overlaps == 0)
            {
                int? bookingId = booking is null ? null : await AddBooking(booking, run, cancellation);
                CalendarEvent created = target with { BookingId = bookingId };
                var newId = await calendar.Add(created, cancellation);
                employeeEvents.Add(created with { Id = newId });
                run.CalendarCounts.Created++;
                continue;
            }

            if (overlaps == 1 && IsIdentical(employeeEvents[sameIndex], target))
            {
                await MergeIdenticalSlot(sameIndex, booking, item, employeeEvents, run, cancellation);
                continue;
            }

            run.SkipSlot(TakeoutWarningCode.CalendarOverlapSkipped, item,
                "Slot overlaps an existing calendar event for the same employee.", withBooking: booking is not null);
        }

        foreach (TakeoutBooking orphan in bookingsById.Values.Where(b => !usedBookings.Contains(b.Id)))
        {
            run.Warn(TakeoutWarningCode.MissingReferenceSkipped, nameof(Booking), orphan.Id,
                "Booking is not attached to any imported calendar slot.");
            run.BookingCounts.Skipped++;
        }
    }

    private async Task MergeIdenticalSlot(int sameIndex, Booking? booking, TakeoutCalendarSlot item,
        List<CalendarEvent> employeeEvents, ImportRun run, CancellationToken cancellation)
    {
        CalendarEvent same = employeeEvents[sameIndex];
        if (booking is null)
        {
            // Vacancy in the document; the existing slot (booked or not) already covers it.
            run.CalendarCounts.Matched++;
            return;
        }

        if (same.BookingId is { } existingBookingId)
        {
            Booking? existingBooking = await bookings.GetById(existingBookingId, cancellation);
            if (existingBooking is not null
                && existingBooking.CustomerId == booking.CustomerId
                && existingBooking.ServiceId == booking.ServiceId)
            {
                run.CalendarCounts.Matched++;
                run.BookingCounts.Matched++;
                return;
            }

            run.SkipSlot(TakeoutWarningCode.CalendarOverlapSkipped, item,
                "An identical slot already exists with a different booking.", withBooking: true);
            return;
        }

        // Identical existing vacancy: book it. `same` comes from this transaction's RepeatableRead snapshot, so a
        // concurrent change surfaces as a failed Update (optimistic concurrency) or a serialization error.
        var bookingId = await AddBooking(booking, run, cancellation);
        CalendarEvent booked = same with { BookingId = bookingId };
        if (!await calendar.Update(booked, cancellation))
        {
            await bookings.Delete(bookingId, cancellation);
            run.BookingCounts.Created--;
            run.SkipSlot(TakeoutWarningCode.CalendarOverlapSkipped, item, "The matching vacancy changed during import.",
                withBooking: true);
            return;
        }

        employeeEvents[sameIndex] = booked;
        run.CalendarCounts.Matched++;
    }

    /// <summary>Counts events overlapping <paramref name="target" /> and returns the index of the last one.</summary>
    private static (int Count, int Index) FindOverlaps(List<CalendarEvent> events, CalendarEvent target)
    {
        (int count, int index) = (0, -1);
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Contains(target))
            {
                (count, index) = (count + 1, i);
            }
        }

        return (count, index);
    }

    private static bool IsIdentical(CalendarEvent existing, CalendarEvent target) =>
        existing.LocationId == target.LocationId
        && existing.StartTime.Equalsish(target.StartTime)
        && existing.EndTime.Equalsish(target.EndTime);

    private async Task<int> AddBooking(Booking booking, ImportRun run, CancellationToken cancellation)
    {
        var id = await bookings.Add(booking, cancellation);
        run.BookingCounts.Created++;
        return id;
    }

    private static Booking? MapBooking(int sourceBookingId, Dictionary<int, TakeoutBooking> bookingsById,
        ImportRun run)
    {
        if (!bookingsById.TryGetValue(sourceBookingId, out TakeoutBooking? source)
            || !run.UserMap.TryGetValue(source.CustomerId, out (int Id, string Role) customer)
            || !run.ServiceMap.TryGetValue(source.ServiceId, out var serviceId))
        {
            return null;
        }

        return new Booking(customer.Id, serviceId, TrimToNull(source.Notes));
    }

    private static List<CalendarEvent> EventsOf(Dictionary<int, List<CalendarEvent>> byEmployee, int employeeId) =>
        CollectionsMarshal.GetValueRefOrAddDefault(byEmployee, employeeId, out _) ??= [];

    private static bool IsKnownRole(string? role) =>
        role is UserRole.Customer or UserRole.Employee or UserRole.Admin;

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
        Justification = "Email addresses are stored and compared in lower case")]
    private static string? NormalizeEmail(string? email) => TrimToNull(email)?.ToLowerInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private sealed class Source(TakeoutDocument document)
    {
        public IReadOnlyList<TakeoutUser> Users { get; } = document.Users ?? [];
        public IReadOnlyList<TakeoutLocation> Locations { get; } = document.Locations ?? [];
        public IReadOnlyList<TakeoutService> Services { get; } = document.Services ?? [];
        public IReadOnlyList<TakeoutBooking> Bookings { get; } = document.Bookings ?? [];
        public IReadOnlyList<TakeoutCalendarSlot> Calendar { get; } = document.Calendar ?? [];

        public long Count => (long)Users.Count + Locations.Count + Services.Count + Bookings.Count + Calendar.Count;
    }

    private sealed class Counts
    {
        public int Created { get; set; }
        public int Matched { get; set; }
        public int Skipped { get; set; }

        public EntityImportCounts ToRecord() => new(Created, Matched, Skipped);
    }

    private sealed class ImportRun(DateTime now)
    {
        public DateTime Now { get; } = now;
        public List<TakeoutWarning> Warnings { get; } = [];

        /// <summary>source id → (target id, role). Targets are only ever RLS-read or freshly inserted ids.</summary>
        public Dictionary<int, (int Id, string Role)> UserMap { get; } = [];

        public Dictionary<int, int> LocationMap { get; } = [];
        public List<int> PrivilegedUsersCreated { get; } = [];
        public Dictionary<int, int> ServiceMap { get; } = [];
        public Counts UserCounts { get; } = new();
        public Counts LocationCounts { get; } = new();
        public Counts ServiceCounts { get; } = new();
        public Counts BookingCounts { get; } = new();
        public Counts CalendarCounts { get; } = new();

        public void MapUser(int sourceId, int targetId, string role) => UserMap[sourceId] = (targetId, role);

        /// <summary>Maps a source user id to a target staff (Employee/Admin) user id.</summary>
        public bool TryMapStaff(int sourceId, out int targetId)
        {
            if (UserMap.TryGetValue(sourceId, out (int Id, string Role) user)
                && user.Role is UserRole.Employee or UserRole.Admin)
            {
                targetId = user.Id;
                return true;
            }

            targetId = 0;
            return false;
        }

        public void Warn(TakeoutWarningCode code, string entity, int? sourceId, string message) =>
            Warnings.Add(new TakeoutWarning(code, entity, sourceId, message));

        public void Matched(string entity, int sourceId, Counts counts, string message)
        {
            Warn(TakeoutWarningCode.MatchedExistingByNaturalKey, entity, sourceId, message);
            counts.Matched++;
        }

        public void SkipDuplicate(string entity, int sourceId, Counts counts)
        {
            Warn(TakeoutWarningCode.DuplicateSourceIdSkipped, entity, sourceId,
                "Duplicate id within the document; entry skipped.");
            counts.Skipped++;
        }

        public void SkipSlot(TakeoutWarningCode code, TakeoutCalendarSlot slot, string message,
            bool withBooking = false)
        {
            Warn(code, nameof(CalendarEvent), slot.Id, message);
            CalendarCounts.Skipped++;
            if (withBooking)
            {
                BookingCounts.Skipped++;
            }
        }

        public ImportTakeoutResponse ToResponse(bool validateOnly) => new(
            validateOnly,
            UserCounts.ToRecord(),
            LocationCounts.ToRecord(),
            ServiceCounts.ToRecord(),
            BookingCounts.ToRecord(),
            CalendarCounts.ToRecord(),
            Warnings);
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(310, LogLevel.Warning, "User {UserId} is not permitted to import takeout")]
        public partial void Forbidden(int userId);

        [LoggerMessage(311, LogLevel.Warning, "User {UserId} tried to import a {Scope} takeout document")]
        public partial void WrongScope(int userId, TakeoutScope scope);

        [LoggerMessage(313, LogLevel.Warning,
            "User {UserId} has an Admin token but is not an active admin in the database; takeout import denied")]
        public partial void NotAdminInDatabase(int userId);

        [LoggerMessage(312, LogLevel.Information,
            "User {UserId} imported takeout (validateOnly={ValidateOnly}) with {WarningCount} warnings")]
        public partial void Imported(int userId, bool validateOnly, int warningCount);
    }
}
