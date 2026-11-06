using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.Takeout;

public sealed partial class ExportTakeoutCommand(
    IUserRepository users,
    ILocationRepository locations,
    IServiceRepository services,
    IBookingRepository bookings,
    ICalendarRepository calendar,
    ITenantContext tenantContext,
    IActivityRecorder activityRecorder,
    TimeProvider timeProvider,
    ILogger<ExportTakeoutCommand> logger) : ICommand<ExportTakeoutRequest, Task<Result<TakeoutDocument>>>
{
    private static readonly PageQuery All = TakeoutQueries.All;

    private static readonly Problem NotAdmin =
        Problem.Forbidden with { Detail = "Only administrators can export the tenant workspace." };

    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<TakeoutDocument>> Execute(ExportTakeoutRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        switch (query.Scope)
        {
            case TakeoutScope.TenantWorkspace:
                return await ExportTenantWorkspace(query, now, cancellation);
            case TakeoutScope.CustomerGdpr:
                return await ExportCustomerGdpr(query, now, cancellation);
            default:
                _log.UnsupportedScope(query.AuthenticatedUserId, query.Scope);
                return Problem.ValidationFailed with { Detail = "Unsupported takeout scope." };
        }
    }

    private async Task<Result<TakeoutDocument>> ExportTenantWorkspace(ExportTakeoutRequest query, DateTime now,
        CancellationToken cancellation)
    {
        if (!query.User.IsInRole(UserRole.Admin))
        {
            _log.ForbiddenWorkspaceExport(query.AuthenticatedUserId);
            return NotAdmin;
        }

        // ASVS 8.3.2: the JWT role claim may be up to one access-token lifetime stale (demoted or deleted
        // admin), so re-authorize against the database before dumping every user's personal data.
        User? caller = await users.GetById(query.AuthenticatedUserId, cancellation);
        // GetById excludes soft-deleted users, so a deleted admin resolves to null here.
        if (caller is not { Role: UserRole.Admin })
        {
            _log.NotAdminInDatabase(query.AuthenticatedUserId);
            return NotAdmin;
        }

        // Repositories already exclude soft-deleted rows; only business filters are applied here.
        List<CalendarEvent> upcomingSlots = await calendar.GetAll(All, cancellation)
            .Where(c => c.EndTime > now)
            .ToListAsync(cancellation);
        HashSet<int> upcomingBookingIds = upcomingSlots.Select(c => c.BookingId).OfType<int>().ToHashSet();
        List<Booking> upcomingBookings = await bookings.GetAll(All, cancellation)
            .Where(b => upcomingBookingIds.Contains(b.Id))
            .ToListAsync(cancellation);
        List<User> allUsers = await users.GetAll(All, cancellation).ToListAsync(cancellation);
        List<Location> allLocations = await locations.GetAll(All, cancellation).ToListAsync(cancellation);
        List<Service> allServices = await services.GetAll(All, cancellation).ToListAsync(cancellation);

        TakeoutDocument document = BuildDocument(now, TakeoutScope.TenantWorkspace,
            allUsers.Select(MapFullUser),
            allLocations.Select(MapLocation),
            allServices.Select(s => MapService(s, s.Employees)),
            upcomingBookings.Select(MapBooking),
            upcomingSlots.Select(MapSlot));
        await LogExport(query, document, cancellation);
        return document;
    }

    private async Task<Result<TakeoutDocument>> ExportCustomerGdpr(ExportTakeoutRequest query, DateTime now,
        CancellationToken cancellation)
    {
        int subjectId = query.AuthenticatedUserId;
        User? subject = await users.GetById(subjectId, cancellation);
        if (subject is null)
        {
            _log.SubjectNotFound(subjectId);
            return Problem.NotFound with { Detail = "The requested user was not found." };
        }

        List<Booking> customerBookings = await bookings.GetAll(All, cancellation)
            .Where(b => b.CustomerId == subjectId)
            .ToListAsync(cancellation);
        HashSet<int> bookingIds = customerBookings.Select(b => b.Id).ToHashSet();
        HashSet<int> serviceIds = customerBookings.Select(b => b.ServiceId).ToHashSet();

        List<CalendarEvent> slots = await calendar.GetAll(All, cancellation)
            .Where(c => c.BookingId is { } bookingId && bookingIds.Contains(bookingId))
            .ToListAsync(cancellation);
        HashSet<int> locationIds = slots.Select(c => c.LocationId).ToHashSet();
        HashSet<int> employeeIds = slots.Select(c => c.EmployeeId).Where(id => id != subjectId).ToHashSet();

        List<Location> referencedLocations = await locations.GetAll(All, cancellation)
            .Where(l => locationIds.Contains(l.Id))
            .ToListAsync(cancellation);
        List<Service> referencedServices = await services.GetAll(All, cancellation)
            .Where(s => serviceIds.Contains(s.Id))
            .ToListAsync(cancellation);
        List<User> employees = await users.GetAll(All, cancellation)
            .Where(u => employeeIds.Contains(u.Id))
            .ToListAsync(cancellation);

        List<TakeoutUser> exportedUsers = [MapFullUser(subject), .. employees.Select(MapRedactedUser)];
        HashSet<int> exportedUserIds = exportedUsers.Select(u => u.Id).ToHashSet();

        TakeoutDocument document = BuildDocument(now, TakeoutScope.CustomerGdpr,
            exportedUsers,
            referencedLocations.Select(MapLocation),
            referencedServices.Select(s => MapService(s, s.Employees.Where(id => exportedUserIds.Contains(id)).ToList())),
            customerBookings.Select(MapBooking),
            slots.Select(MapSlot));
        await LogExport(query, document, cancellation);
        return document;
    }

    private TakeoutDocument BuildDocument(DateTime now, TakeoutScope scope,
        IEnumerable<TakeoutUser> exportUsers,
        IEnumerable<TakeoutLocation> exportLocations,
        IEnumerable<TakeoutService> exportServices,
        IEnumerable<TakeoutBooking> exportBookings,
        IEnumerable<TakeoutCalendarSlot> exportSlots) =>
        new(TakeoutDocument.CurrentSchemaVersion, now, tenantContext.Slug, scope,
            exportUsers.OrderBy(x => x.Id).ToList(),
            exportLocations.OrderBy(x => x.Id).ToList(),
            exportServices.OrderBy(x => x.Id).ToList(),
            exportBookings.OrderBy(x => x.Id).ToList(),
            exportSlots.OrderBy(x => x.Id).ToList());

    /// <summary>ASVS 16.3.2: bulk personal-data access is audited (who, when, which scope) — never the data itself.</summary>
    private async Task LogExport(ExportTakeoutRequest query, TakeoutDocument document, CancellationToken cancellation)
    {
        var userId = query.AuthenticatedUserId;
        _log.Exported(userId, document.Scope, document.Users.Count, document.Locations.Count,
            document.Services.Count, document.Bookings.Count, document.Calendar.Count);
        await activityRecorder.Export<TakeoutDocument>(new(userId, 0, query.TenantId), cancellation);
    }

    private static TakeoutUser MapFullUser(User user) =>
        new(user.Id, user.Role, user.Email, user.Name, user.Phone);

    private static TakeoutUser MapRedactedUser(User user) =>
        new(user.Id, user.Role, Email: null, Name: user.Name, Phone: null);

    private static TakeoutLocation MapLocation(Location location) =>
        new(location.Id, location.Name, location.Address1, location.Address2, location.Zip, location.City);

    private static TakeoutService MapService(Service service, IReadOnlyList<int> employeeIds) =>
        new(service.Id, service.Name, service.Duration, employeeIds, service.Description);

    private static TakeoutBooking MapBooking(Booking booking) =>
        new(booking.Id, booking.CustomerId, booking.ServiceId, booking.Notes);

    private static TakeoutCalendarSlot MapSlot(CalendarEvent slot) =>
        new(slot.Id, slot.EmployeeId, slot.LocationId, slot.BookingId, slot.StartTime, slot.EndTime);

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(300, LogLevel.Warning, "User {UserId} is not permitted to export the tenant workspace")]
        public partial void ForbiddenWorkspaceExport(int userId);

        [LoggerMessage(301, LogLevel.Information,
            "User {UserId} exported {Scope}: {Users} users, {Locations} locations, {Services} services, {Bookings} bookings, {Calendar} calendar slots")]
        public partial void Exported(int userId, TakeoutScope scope, int users, int locations, int services, int bookings, int calendar);

        [LoggerMessage(302, LogLevel.Warning, "User {UserId} requested a GDPR export but was not found")]
        public partial void SubjectNotFound(int userId);

        [LoggerMessage(304, LogLevel.Warning,
            "User {UserId} has an Admin token but is not an active admin in the database; workspace export denied")]
        public partial void NotAdminInDatabase(int userId);

        [LoggerMessage(303, LogLevel.Warning, "User {UserId} requested unsupported takeout scope {Scope}")]
        public partial void UnsupportedScope(int userId, TakeoutScope scope);
    }
}
