using System.Text.Json;
using Klinkby.Booqr.Application.Commands.Takeout;

namespace Klinkby.Booqr.Application.Tests.Commands.Takeout;

public class ExportTakeoutCommandTests
{
    private const int AdminCallerId = 1;
    private const int CustomerSubjectId = 11;
    private static readonly int[] WorkspaceCalendarIds = [1, 3];
    private static readonly int[] WorkspaceBookingIds = [1];
    private static readonly int[] WorkspaceEmployeeIds = [10];
    private static readonly int[] GdprBookingIds = [1, 2];
    private static readonly int[] GdprCalendarIds = [1, 2];
    private static readonly int[] GdprLocationIds = [1];
    private static readonly int[] GdprServiceEmployeeIds = [10];
    private static readonly int[] GdprUserIds = [10, 11];
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly Mock<ILocationRepository> _locations = new(MockBehavior.Strict);
    private readonly Mock<IServiceRepository> _services = new(MockBehavior.Strict);
    private readonly Mock<IBookingRepository> _bookings = new(MockBehavior.Strict);
    private readonly Mock<ICalendarRepository> _calendar = new(MockBehavior.Strict);
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    private ExportTakeoutCommand CreateSut()
    {
        return new ExportTakeoutCommand(
            _users.Object,
            _locations.Object,
            _services.Object,
            _bookings.Object,
            _calendar.Object,
            CreateTenantContext(),
            _activityRecorder.Object,
            TestHelpers.TimeProvider,
            NullLogger<ExportTakeoutCommand>.Instance);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    public async Task GIVEN_NonAdmin_WHEN_ExportTenantWorkspace_THEN_ReturnsForbidden_And_DoesNotQueryRepositories(string role)
    {
        // Arrange
        var request = new ExportTakeoutRequest(TakeoutScope.TenantWorkspace) { User = CreateUser(42, role) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        var fault = Assert.IsType<Result<TakeoutDocument>.Fault>(result);
        Assert.Equal(Problem.Forbidden.Type, fault.Problem.Type);
        _users.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendar.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Admin_WHEN_ExportTenantWorkspace_THEN_ExportsOnlyUpcomingSlotsAndTheirBookings(
        DateTime t0,
        User admin,
        User employee,
        User customer,
        Location location,
        Service service,
        Booking upcomingBooking,
        Booking pastBooking,
        CalendarEvent upcomingSlot,
        CalendarEvent pastSlot,
        CalendarEvent openSlot)
    {
        // Arrange
        admin = admin with { Id = AdminCallerId, Role = UserRole.Admin, Deleted = null };
        employee = employee with { Id = 10, Role = UserRole.Employee, Deleted = null };
        customer = customer with { Id = 11, Role = UserRole.Customer, Deleted = null };
        location = location with { Id = 1, Deleted = null };
        service = service with { Id = 1, Employees = [10], Deleted = null };
        upcomingBooking = upcomingBooking with { Id = 1, CustomerId = 11, ServiceId = 1, Deleted = null };
        pastBooking = pastBooking with { Id = 2, CustomerId = 11, ServiceId = 1, Deleted = null };
        upcomingSlot = upcomingSlot with
        {
            Id = 1, EmployeeId = 10, LocationId = 1, BookingId = 1,
            StartTime = t0.AddHours(1), EndTime = t0.AddHours(2), Deleted = null
        };
        pastSlot = pastSlot with
        {
            Id = 2, EmployeeId = 10, LocationId = 1, BookingId = 2,
            StartTime = t0.AddHours(-2), EndTime = t0, Deleted = null
        };
        openSlot = openSlot with
        {
            Id = 3, EmployeeId = 10, LocationId = 1, BookingId = null,
            StartTime = t0.AddHours(3), EndTime = t0.AddHours(4), Deleted = null
        };

        _users.Setup(x => x.GetById(AdminCallerId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _users.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(employee, customer));
        _locations.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(location));
        _services.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(service));
        _bookings.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(upcomingBooking, pastBooking));
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(upcomingSlot, pastSlot, openSlot));

        var request = new ExportTakeoutRequest(TakeoutScope.TenantWorkspace) { User = CreateUser(AdminCallerId, UserRole.Admin) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        TakeoutDocument doc = Assert.IsType<Result<TakeoutDocument>.Success>(result).Value;
        Assert.Equal(TakeoutScope.TenantWorkspace, doc.Scope);
        Assert.Equal(TakeoutDocument.CurrentSchemaVersion, doc.SchemaVersion);
        Assert.Equal(TenantSlug, doc.TenantSlug);
        Assert.Equal(t0, doc.ExportedAt);
        Assert.Equal(WorkspaceCalendarIds, doc.Calendar.Select(c => c.Id).ToArray());
        Assert.Equal(WorkspaceBookingIds, doc.Bookings.Select(b => b.Id).ToArray());
        Assert.Equal(WorkspaceEmployeeIds, doc.Services.Single().EmployeeIds.ToArray());
        Assert.Equal(employee.Email, doc.Users.Single(u => u.Id == 10).Email);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Admin_WHEN_ExportTenantWorkspace_THEN_PasswordHashIsNotSerialized(
        User admin,
        User employee)
    {
        // Arrange
        admin = admin with { Id = AdminCallerId, Role = UserRole.Admin, Deleted = null };
        employee = employee with { Id = 10, Role = UserRole.Employee, Deleted = null };
        Assert.NotNull(employee.PasswordHash);
        _users.Setup(x => x.GetById(AdminCallerId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _users.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield(employee));
        _locations.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Location>());
        _services.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Service>());
        _bookings.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Booking>());
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<CalendarEvent>());
        var request = new ExportTakeoutRequest(TakeoutScope.TenantWorkspace) { User = CreateUser(AdminCallerId, UserRole.Admin) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        TakeoutDocument doc = Assert.IsType<Result<TakeoutDocument>.Success>(result).Value;
        string json = JsonSerializer.Serialize(doc, WebOptions);
        Assert.DoesNotContain(employee.PasswordHash, json, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AdminClaimButDatabaseRoleIsCustomer_WHEN_ExportTenantWorkspace_THEN_ReturnsForbidden_And_DoesNotQueryRepositories(
        User caller)
    {
        // Arrange
        caller = caller with { Id = AdminCallerId, Role = UserRole.Customer, Deleted = null };
        _users.Setup(x => x.GetById(AdminCallerId, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        var request = new ExportTakeoutRequest(TakeoutScope.TenantWorkspace) { User = CreateUser(AdminCallerId, UserRole.Admin) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        var fault = Assert.IsType<Result<TakeoutDocument>.Fault>(result);
        Assert.Equal(Problem.Forbidden.Type, fault.Problem.Type);
        _users.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _locations.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _services.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _bookings.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendar.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _activityRecorder.Verify(x => x.Export<TakeoutDocument>(It.IsAny<ActivityQuery<TakeoutDocument>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AdminClaimButDatabaseUserSoftDeleted_WHEN_ExportTenantWorkspace_THEN_ReturnsForbidden_And_DoesNotQueryRepositories(
        DateTime t0,
        User caller)
    {
        // Arrange
        caller = caller with { Id = AdminCallerId, Role = UserRole.Admin, Deleted = t0 };
        _users.Setup(x => x.GetById(AdminCallerId, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        var request = new ExportTakeoutRequest(TakeoutScope.TenantWorkspace) { User = CreateUser(AdminCallerId, UserRole.Admin) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        var fault = Assert.IsType<Result<TakeoutDocument>.Fault>(result);
        Assert.Equal(Problem.Forbidden.Type, fault.Problem.Type);
        _users.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendar.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _activityRecorder.Verify(x => x.Export<TakeoutDocument>(It.IsAny<ActivityQuery<TakeoutDocument>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AdminClaimButDatabaseUserNotFound_WHEN_ExportTenantWorkspace_THEN_ReturnsForbidden_And_DoesNotQueryRepositories()
    {
        // Arrange
        _users.Setup(x => x.GetById(AdminCallerId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var request = new ExportTakeoutRequest(TakeoutScope.TenantWorkspace) { User = CreateUser(AdminCallerId, UserRole.Admin) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        var fault = Assert.IsType<Result<TakeoutDocument>.Fault>(result);
        Assert.Equal(Problem.Forbidden.Type, fault.Problem.Type);
        _users.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendar.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _activityRecorder.Verify(x => x.Export<TakeoutDocument>(It.IsAny<ActivityQuery<TakeoutDocument>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AdminWorkspaceExport_WHEN_Execute_THEN_RecordsExportOnceForCaller(User admin)
    {
        // Arrange
        admin = admin with { Id = AdminCallerId, Role = UserRole.Admin, Deleted = null };
        _users.Setup(x => x.GetById(AdminCallerId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _users.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<User>());
        _locations.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Location>());
        _services.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Service>());
        _bookings.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Booking>());
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<CalendarEvent>());
        var request = new ExportTakeoutRequest(TakeoutScope.TenantWorkspace) { User = CreateUser(AdminCallerId, UserRole.Admin) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<TakeoutDocument>.Success>(result);
        _activityRecorder.Verify(x => x.Export<TakeoutDocument>(
            It.Is<ActivityQuery<TakeoutDocument>>(q => q.UserId == AdminCallerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerGdpr_WHEN_Execute_THEN_ExportsOwnHistoryAndRedactsEmployees(
        DateTime t0,
        User subject,
        User employee,
        User otherCustomer,
        Location usedLocation,
        Location unusedLocation,
        Service usedService,
        Service unusedService,
        Booking subjectPastBooking,
        Booking subjectUpcomingBooking,
        Booking otherBooking,
        CalendarEvent subjectPastSlot,
        CalendarEvent subjectUpcomingSlot,
        CalendarEvent otherSlot)
    {
        // Arrange
        subject = subject with { Id = 11, Role = UserRole.Customer, Deleted = null };
        employee = employee with { Id = 10, Role = UserRole.Employee, Deleted = null };
        otherCustomer = otherCustomer with { Id = 12, Role = UserRole.Customer, Deleted = null };
        usedLocation = usedLocation with { Id = 1, Deleted = null };
        unusedLocation = unusedLocation with { Id = 2, Deleted = null };
        usedService = usedService with { Id = 1, Employees = [10, 99], Deleted = null };
        unusedService = unusedService with { Id = 2, Employees = [10], Deleted = null };
        subjectPastBooking = subjectPastBooking with { Id = 1, CustomerId = 11, ServiceId = 1, Deleted = null };
        subjectUpcomingBooking = subjectUpcomingBooking with { Id = 2, CustomerId = 11, ServiceId = 1, Deleted = null };
        otherBooking = otherBooking with { Id = 3, CustomerId = 12, ServiceId = 2, Deleted = null };
        subjectPastSlot = subjectPastSlot with
        {
            Id = 1, EmployeeId = 10, LocationId = 1, BookingId = 1,
            StartTime = t0.AddDays(-2), EndTime = t0.AddDays(-2).AddHours(1), Deleted = null
        };
        subjectUpcomingSlot = subjectUpcomingSlot with
        {
            Id = 2, EmployeeId = 10, LocationId = 1, BookingId = 2,
            StartTime = t0.AddHours(1), EndTime = t0.AddHours(2), Deleted = null
        };
        otherSlot = otherSlot with
        {
            Id = 3, EmployeeId = 10, LocationId = 2, BookingId = 3,
            StartTime = t0.AddHours(3), EndTime = t0.AddHours(4), Deleted = null
        };

        _users.Setup(x => x.GetById(subject.Id, It.IsAny<CancellationToken>())).ReturnsAsync(subject);
        _users.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(subject, employee, otherCustomer));
        _locations.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(usedLocation, unusedLocation));
        _services.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(usedService, unusedService));
        _bookings.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(subjectPastBooking, subjectUpcomingBooking, otherBooking));
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(subjectPastSlot, subjectUpcomingSlot, otherSlot));

        var request = new ExportTakeoutRequest(TakeoutScope.CustomerGdpr) { User = CreateUser(subject.Id, UserRole.Customer) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        TakeoutDocument doc = Assert.IsType<Result<TakeoutDocument>.Success>(result).Value;
        Assert.Equal(TakeoutScope.CustomerGdpr, doc.Scope);
        Assert.Equal(GdprBookingIds, doc.Bookings.Select(b => b.Id).ToArray());
        Assert.Equal(GdprCalendarIds, doc.Calendar.Select(c => c.Id).ToArray());
        Assert.Equal(GdprLocationIds, doc.Locations.Select(l => l.Id).ToArray());
        TakeoutService service = Assert.Single(doc.Services);
        Assert.Equal(1, service.Id);
        Assert.Equal(GdprServiceEmployeeIds, service.EmployeeIds.ToArray());
        Assert.Equal(GdprUserIds, doc.Users.Select(u => u.Id).ToArray());

        TakeoutUser exportedSubject = doc.Users.Single(u => u.Id == subject.Id);
        Assert.Equal(subject.Email, exportedSubject.Email);
        Assert.Equal(subject.Phone, exportedSubject.Phone);

        TakeoutUser exportedEmployee = doc.Users.Single(u => u.Id == employee.Id);
        Assert.Null(exportedEmployee.Email);
        Assert.Null(exportedEmployee.Phone);
        Assert.Equal(employee.Name, exportedEmployee.Name);
    }

    [Fact]
    public async Task GIVEN_CustomerGdprSubjectNotFound_WHEN_Execute_THEN_ReturnsNotFound_And_DoesNotExportData()
    {
        // Arrange
        _users.Setup(x => x.GetById(42, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var request = new ExportTakeoutRequest(TakeoutScope.CustomerGdpr) { User = CreateUser() };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        var fault = Assert.IsType<Result<TakeoutDocument>.Fault>(result);
        Assert.Equal(Problem.NotFound.Type, fault.Problem.Type);
        _bookings.Verify(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerWithPasswordHash_WHEN_ExportGdpr_THEN_PasswordHashIsNotSerialized(
        User subject)
    {
        // Arrange
        subject = subject with { Id = 11, Role = UserRole.Customer, Deleted = null };
        Assert.NotNull(subject.PasswordHash);
        _users.Setup(x => x.GetById(subject.Id, It.IsAny<CancellationToken>())).ReturnsAsync(subject);
        _users.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<User>());
        _locations.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Location>());
        _services.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Service>());
        _bookings.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Booking>());
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<CalendarEvent>());
        var request = new ExportTakeoutRequest(TakeoutScope.CustomerGdpr) { User = CreateUser(subject.Id, UserRole.Customer) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        TakeoutDocument doc = Assert.IsType<Result<TakeoutDocument>.Success>(result).Value;
        string json = JsonSerializer.Serialize(doc, WebOptions);
        Assert.DoesNotContain(subject.PasswordHash, json, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerGdprExport_WHEN_Execute_THEN_RecordsExportOnceForCaller(User subject)
    {
        // Arrange
        subject = subject with { Id = CustomerSubjectId, Role = UserRole.Customer, Deleted = null };
        _users.Setup(x => x.GetById(CustomerSubjectId, It.IsAny<CancellationToken>())).ReturnsAsync(subject);
        _users.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<User>());
        _locations.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Location>());
        _services.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Service>());
        _bookings.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<Booking>());
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>())).Returns(Yield<CalendarEvent>());
        var request = new ExportTakeoutRequest(TakeoutScope.CustomerGdpr) { User = CreateUser(CustomerSubjectId, UserRole.Customer) };
        ExportTakeoutCommand sut = CreateSut();

        // Act
        Result<TakeoutDocument> result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<TakeoutDocument>.Success>(result);
        _activityRecorder.Verify(x => x.Export<TakeoutDocument>(
            It.Is<ActivityQuery<TakeoutDocument>>(q => q.UserId == CustomerSubjectId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
