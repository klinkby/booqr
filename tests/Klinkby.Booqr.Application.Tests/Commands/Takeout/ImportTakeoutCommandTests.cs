using System.Diagnostics.CodeAnalysis;
using System.Data;
using Klinkby.Booqr.Application.Commands.Takeout;

namespace Klinkby.Booqr.Application.Tests.Commands.Takeout;

public class ImportTakeoutCommandTests
{
    private const int EmployeeSourceId = 101;
    private const int CustomerSourceId = 102;
    private const int LocationSourceId = 103;
    private const int ServiceSourceId = 104;
    private const int BookingSourceId = 105;
    private const int BookedSlotSourceId = 106;
    private const int VacancySlotSourceId = 107;
    private const int AdminSourceId = 108;
    private const int CallerId = 42;
    private const int OtherUserId = 99;
    private static readonly TimeSpan SlotLength = TimeSpan.FromHours(1);
    private static readonly string[] NonAdminRoles = [UserRole.Customer, UserRole.Employee];
    private static readonly int[] CallerIds = [CallerId];
    private static readonly int[] OtherUserIds = [OtherUserId];

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ILocationRepository> _locations = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IEmployeeServiceRepository> _employeeServices = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<ICalendarRepository> _calendar = new();
    private readonly Mock<ITransaction> _transaction = new();
    private readonly Mock<IActivityRecorder> _activityRecorder = new();
    private readonly ImportTakeoutCommand _sut;

    public ImportTakeoutCommandTests()
    {
        _users.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield<User>());
        _locations.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield<Location>());
        _services.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield<Service>());
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield<CalendarEvent>());
        _users.Setup(x => x.LockAdmins(It.IsAny<CancellationToken>())).ReturnsAsync(CallerIds);

        _sut = new ImportTakeoutCommand(
            _users.Object,
            _locations.Object,
            _services.Object,
            _employeeServices.Object,
            _bookings.Object,
            _calendar.Object,
            _transaction.Object,
            _activityRecorder.Object,
            TestHelpers.TimeProvider,
            NullLogger<ImportTakeoutCommand>.Instance);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NonAdminUser_WHEN_Execute_THEN_ReturnsForbiddenWithoutTouchingStorage(
        TakeoutDocument document,
        int userId)
    {
        foreach (string role in NonAdminRoles)
        {
            ImportTakeoutRequest request = CreateRequest(document, user: CreateUser(userId, role));

            var result = await _sut.Execute(request);

            var fault = Assert.IsType<Fault>(result.Value);
            Assert.Equal(Problem.Forbidden.Type, fault.Problem.Type);
        }

        _transaction.Verify(x => x.Begin(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.VerifyNoOtherCalls();
        _locations.VerifyNoOtherCalls();
        _services.VerifyNoOtherCalls();
        _employeeServices.VerifyNoOtherCalls();
        _bookings.VerifyNoOtherCalls();
        _calendar.VerifyNoOtherCalls();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerGdprDocument_WHEN_Execute_THEN_ReturnsValidationFaultWithoutBegin(
        TakeoutDocument document)
    {
        TakeoutDocument gdpr = document with { Scope = TakeoutScope.CustomerGdpr };

        var result = await _sut.Execute(CreateRequest(gdpr));

        var fault = Assert.IsType<Fault>(result.Value);
        Assert.Equal(Problem.ValidationFailed.Type, fault.Problem.Type);
        _transaction.Verify(x => x.Begin(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CleanDocumentIntoEmptyTenant_WHEN_Execute_THEN_CreatesEntitiesWithRemappedForeignKeys(
        DateTime t0,
        int employeeId,
        int customerId,
        int locationId,
        int serviceId,
        int bookingId,
        string employeeLocal,
        string customerLocal)
    {
        string employeeMail = MailOf(employeeLocal);
        string customerMail = MailOf(customerLocal);
        DateTime bookedStart = t0.AddDays(1);
        DateTime vacancyStart = t0.AddDays(2);
        TakeoutCalendarSlot booked = new(BookedSlotSourceId, EmployeeSourceId, LocationSourceId, BookingSourceId,
            bookedStart, bookedStart.Add(SlotLength));
        TakeoutCalendarSlot vacancy = new(VacancySlotSourceId, EmployeeSourceId, LocationSourceId, null,
            vacancyStart, vacancyStart.Add(SlotLength));
        TakeoutDocument document = CreateBookingDocument(t0, employeeMail, customerMail, [booked, vacancy]);

        _users.Setup(x => x.Add(It.Is<User>(u => u.Email == employeeMail), It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);
        _users.Setup(x => x.Add(It.Is<User>(u => u.Email == customerMail), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerId);
        _locations.Setup(x => x.Add(It.IsAny<Location>(), It.IsAny<CancellationToken>())).ReturnsAsync(locationId);
        _services.Setup(x => x.Add(It.IsAny<Service>(), It.IsAny<CancellationToken>())).ReturnsAsync(serviceId);
        _bookings.Setup(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>())).ReturnsAsync(bookingId);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Equal(2, success.Value.Warnings.Count(w => w.Code == TakeoutWarningCode.UserCreatedWithoutPassword));
        _users.Verify(x => x.Add(
            It.Is<User>(u => u.Email == employeeMail && u.Role == UserRole.Employee && u.PasswordHash == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(x => x.Add(
            It.Is<User>(u => u.Email == customerMail && u.Role == UserRole.Customer && u.PasswordHash == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _bookings.Verify(x => x.Add(
            It.Is<Booking>(b => b.CustomerId == customerId && b.ServiceId == serviceId),
            It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.Add(
            It.Is<CalendarEvent>(e => e.EmployeeId == employeeId && e.LocationId == locationId
                                      && e.BookingId == bookingId),
            It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.Add(
            It.Is<CalendarEvent>(e => e.EmployeeId == employeeId && e.LocationId == locationId
                                      && e.BookingId == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _employeeServices.Verify(x => x.Assign(
            serviceId,
            It.Is<int[]>(ids => ids.SequenceEqual(new[] { employeeId })),
            It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_DocumentAdminMatchesExistingEmailInDifferentCase_WHEN_Execute_THEN_ExistingUserUnchanged(
        DateTime t0,
        User existing,
        int existingId)
    {
        User stored = existing with
        {
            Id = existingId,
            Email = "owner@example.test",
            Role = UserRole.Customer,
            Deleted = null
        };
        _users.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield(stored));
        var admin = new TakeoutUser(AdminSourceId, UserRole.Admin, "OWNER@example.TEST", "Ann");
        TakeoutDocument document = CreateDocument(t0, users: [admin]);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Contains(success.Value.Warnings, w => w.Code == TakeoutWarningCode.MatchedExistingByNaturalKey);
        _users.Verify(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.Update(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.UpdateProfile(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_SlotEmployeeIsCustomerInDocument_WHEN_Execute_THEN_SlotSkippedWithMissingReference(
        DateTime t0,
        int customerId,
        int locationId)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(customerId);
        _locations.Setup(x => x.Add(It.IsAny<Location>(), It.IsAny<CancellationToken>())).ReturnsAsync(locationId);
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, "customer@example.test");
        var location = new TakeoutLocation(LocationSourceId, "Main Street");
        var slot = new TakeoutCalendarSlot(VacancySlotSourceId, CustomerSourceId, LocationSourceId, null,
            t0.AddDays(1), t0.AddDays(1).Add(SlotLength));
        TakeoutDocument document = CreateDocument(t0, users: [customer], locations: [location], calendar: [slot]);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Contains(success.Value.Warnings, w =>
            w.Code == TakeoutWarningCode.MissingReferenceSkipped && w.Entity == nameof(CalendarEvent));
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_SlotReferencesIdsNotInDocument_WHEN_Execute_THEN_SlotsSkippedWithoutRawIds(
        DateTime t0,
        int employeeId,
        int locationId)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        _locations.Setup(x => x.Add(It.IsAny<Location>(), It.IsAny<CancellationToken>())).ReturnsAsync(locationId);
        var employee = new TakeoutUser(EmployeeSourceId, UserRole.Employee, "staff@example.test");
        var location = new TakeoutLocation(LocationSourceId, "Main Street");
        const int unknownEmployeeId = 555;
        const int unknownLocationId = 999;
        var missingEmployee = new TakeoutCalendarSlot(BookedSlotSourceId, unknownEmployeeId, LocationSourceId, null,
            t0.AddDays(1), t0.AddDays(1).Add(SlotLength));
        var missingLocation = new TakeoutCalendarSlot(VacancySlotSourceId, EmployeeSourceId, unknownLocationId, null,
            t0.AddDays(2), t0.AddDays(2).Add(SlotLength));
        TakeoutDocument document = CreateDocument(t0, users: [employee], locations: [location],
            calendar: [missingEmployee, missingLocation]);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Equal(2, success.Value.Warnings.Count(w =>
            w.Code == TakeoutWarningCode.MissingReferenceSkipped && w.Entity == nameof(CalendarEvent)));
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _bookings.Verify(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ExistingEventOverlapsSlotForSameEmployee_WHEN_Execute_THEN_SlotAndBookingSkipped(
        DateTime t0,
        CalendarEvent existing,
        int employeeId,
        int locationId,
        int customerId,
        int serviceId)
    {
        _users.Setup(x => x.Add(It.Is<User>(u => u.Role == UserRole.Employee), It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);
        _users.Setup(x => x.Add(It.Is<User>(u => u.Role == UserRole.Customer), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerId);
        _locations.Setup(x => x.Add(It.IsAny<Location>(), It.IsAny<CancellationToken>())).ReturnsAsync(locationId);
        _services.Setup(x => x.Add(It.IsAny<Service>(), It.IsAny<CancellationToken>())).ReturnsAsync(serviceId);
        CalendarEvent overlapping = existing with
        {
            EmployeeId = employeeId,
            LocationId = locationId,
            BookingId = null,
            StartTime = t0.AddDays(1).AddMinutes(30),
            EndTime = t0.AddDays(1).AddHours(2),
            Deleted = null
        };
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield(overlapping));
        TakeoutCalendarSlot booked = new(BookedSlotSourceId, EmployeeSourceId, LocationSourceId, BookingSourceId,
            t0.AddDays(1), t0.AddDays(1).Add(SlotLength));
        TakeoutDocument document = CreateBookingDocument(t0, "staff@example.test", "guest@example.test", [booked]);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Contains(success.Value.Warnings, w => w.Code == TakeoutWarningCode.CalendarOverlapSkipped);
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _bookings.Verify(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_IdenticalExistingVacancy_WHEN_Execute_THEN_MatchedWithoutAdd(
        DateTime t0,
        CalendarEvent existing,
        int employeeId,
        int locationId)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        _locations.Setup(x => x.Add(It.IsAny<Location>(), It.IsAny<CancellationToken>())).ReturnsAsync(locationId);
        DateTime start = t0.AddDays(1);
        CalendarEvent vacancy = existing with
        {
            EmployeeId = employeeId,
            LocationId = locationId,
            BookingId = null,
            StartTime = start,
            EndTime = start.Add(SlotLength),
            Deleted = null
        };
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield(vacancy));
        var employee = new TakeoutUser(EmployeeSourceId, UserRole.Employee, "staff@example.test");
        var location = new TakeoutLocation(LocationSourceId, "Main Street");
        var slot = new TakeoutCalendarSlot(VacancySlotSourceId, EmployeeSourceId, LocationSourceId, null,
            start, start.Add(SlotLength));
        TakeoutDocument document = CreateDocument(t0, users: [employee], locations: [location], calendar: [slot]);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Equal(1, success.Value.Calendar.Matched);
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _bookings.Verify(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_IdenticalExistingVacancyAndBookedSlot_WHEN_Execute_THEN_BooksExistingVacancy(
        DateTime t0,
        CalendarEvent existing,
        int employeeId,
        int locationId,
        int customerId,
        int serviceId,
        int bookingId)
    {
        _users.Setup(x => x.Add(It.Is<User>(u => u.Role == UserRole.Employee), It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);
        _users.Setup(x => x.Add(It.Is<User>(u => u.Role == UserRole.Customer), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerId);
        _locations.Setup(x => x.Add(It.IsAny<Location>(), It.IsAny<CancellationToken>())).ReturnsAsync(locationId);
        _services.Setup(x => x.Add(It.IsAny<Service>(), It.IsAny<CancellationToken>())).ReturnsAsync(serviceId);
        _bookings.Setup(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>())).ReturnsAsync(bookingId);
        DateTime start = t0.AddDays(1);
        CalendarEvent vacancy = existing with
        {
            EmployeeId = employeeId,
            LocationId = locationId,
            BookingId = null,
            StartTime = start,
            EndTime = start.Add(SlotLength),
            Deleted = null
        };
        _calendar.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield(vacancy));
        _calendar.Setup(x => x.GetById(vacancy.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vacancy);
        _calendar.Setup(x => x.Update(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        TakeoutCalendarSlot booked = new(BookedSlotSourceId, EmployeeSourceId, LocationSourceId, BookingSourceId,
            start, start.Add(SlotLength));
        TakeoutDocument document = CreateBookingDocument(t0, "staff@example.test", "guest@example.test", [booked]);

        var result = await _sut.Execute(CreateRequest(document));

        Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        _bookings.Verify(x => x.Add(
            It.Is<Booking>(b => b.CustomerId == customerId && b.ServiceId == serviceId),
            It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.Update(
            It.Is<CalendarEvent>(e => e.Id == vacancy.Id && e.BookingId == bookingId),
            It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ExistingServiceMatchedByName_WHEN_Execute_THEN_AssignsUnionOfEmployees(
        DateTime t0,
        Service service,
        int existingEmployeeId,
        int employeeId)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        Service existing = service with { Employees = [existingEmployeeId], Deleted = null };
        _services.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => TestHelpers.Yield(existing));
        var employee = new TakeoutUser(EmployeeSourceId, UserRole.Employee, "staff@example.test");
        var duplicateName = new TakeoutService(ServiceSourceId, existing.Name, SlotLength, Single(EmployeeSourceId));
        TakeoutDocument document = CreateDocument(t0, users: [employee], services: [duplicateName]);

        var result = await _sut.Execute(CreateRequest(document));

        Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        _employeeServices.Verify(x => x.Assign(
            existing.Id,
            It.Is<int[]>(ids => ids.Length == 2 && ids.Contains(existingEmployeeId) && ids.Contains(employeeId)),
            It.IsAny<CancellationToken>()), Times.Once);
        _services.Verify(x => x.Add(It.IsAny<Service>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_PastAndInvertedSlots_WHEN_Execute_THEN_SkipsWithTimeWarnings(
        DateTime t0,
        int employeeId,
        int locationId)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        _locations.Setup(x => x.Add(It.IsAny<Location>(), It.IsAny<CancellationToken>())).ReturnsAsync(locationId);
        var employee = new TakeoutUser(EmployeeSourceId, UserRole.Employee, "staff@example.test");
        var location = new TakeoutLocation(LocationSourceId, "Main Street");
        var past = new TakeoutCalendarSlot(BookedSlotSourceId, EmployeeSourceId, LocationSourceId, null,
            t0.AddDays(-1), t0.AddHours(-1));
        var inverted = new TakeoutCalendarSlot(VacancySlotSourceId, EmployeeSourceId, LocationSourceId, null,
            t0.AddDays(1), t0.AddDays(1).AddHours(-1));
        TakeoutDocument document = CreateDocument(t0, users: [employee], locations: [location],
            calendar: [past, inverted]);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Contains(success.Value.Warnings, w => w.Code == TakeoutWarningCode.HistoricalEntrySkipped);
        Assert.Contains(success.Value.Warnings, w => w.Code == TakeoutWarningCode.InvalidTimeRangeSkipped);
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_SchemaVersionTwo_WHEN_Execute_THEN_WarnsAndStillImports(
        DateTime t0,
        int customerId)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(customerId);
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, "customer@example.test");
        TakeoutDocument document = CreateDocument(t0, users: [customer], schemaVersion: 2);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Contains(success.Value.Warnings, w => w.Code == TakeoutWarningCode.SchemaVersionMismatch);
        _users.Verify(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task GIVEN_DryRunFlag_WHEN_Execute_THEN_RollsBackWithoutActivityOnlyWhenDryRun(
        bool dryRun,
        DateTime t0,
        int customerId)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(customerId);
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, "customer@example.test");
        TakeoutDocument document = CreateDocument(t0, users: [customer]);

        var result = await _sut.Execute(CreateRequest(document, dryRun));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Equal(dryRun, success.Value.DryRun);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()),
            dryRun ? Times.Once : Times.Never);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()),
            dryRun ? Times.Never : Times.Once);
        _activityRecorder.Verify(x => x.Import<TakeoutDocument>(It.IsAny<ActivityQuery<TakeoutDocument>>(),
            It.IsAny<CancellationToken>()), dryRun ? Times.Never : Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_RepositoryThrowsMidImport_WHEN_Execute_THEN_RollsBackAndRethrows(
        DateTime t0,
        Exception testException)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ThrowsAsync(testException);
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, "customer@example.test");
        TakeoutDocument document = CreateDocument(t0, users: [customer]);

        Exception thrown = await Assert.ThrowsAsync<Exception>(() => _sut.Execute(CreateRequest(document)));

        Assert.Same(testException, thrown);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
        _activityRecorder.Verify(x => x.Import<TakeoutDocument>(It.IsAny<ActivityQuery<TakeoutDocument>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_DuplicateSourceIdsInDocument_WHEN_Execute_THEN_SecondIsSkipped(
        DateTime t0,
        int customerId,
        string firstLocal,
        string secondLocal)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(customerId);
        var first = new TakeoutUser(CustomerSourceId, UserRole.Customer, MailOf(firstLocal));
        var second = new TakeoutUser(CustomerSourceId, UserRole.Customer, MailOf(secondLocal));
        TakeoutDocument document = CreateDocument(t0, users: [first, second]);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Contains(success.Value.Warnings, w => w.Code == TakeoutWarningCode.DuplicateSourceIdSkipped);
        Assert.Equal(1, success.Value.Users.Created);
        _users.Verify(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_BookingNotReferencedBySlot_WHEN_Execute_THEN_BookingSkippedWithMissingReference(
        DateTime t0,
        int customerId,
        int serviceId)
    {
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(customerId);
        _services.Setup(x => x.Add(It.IsAny<Service>(), It.IsAny<CancellationToken>())).ReturnsAsync(serviceId);
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, "customer@example.test");
        var service = new TakeoutService(ServiceSourceId, "Haircut", SlotLength, []);
        var booking = new TakeoutBooking(BookingSourceId, CustomerSourceId, ServiceSourceId, null);
        TakeoutDocument document = CreateDocument(t0, users: [customer], services: [service], bookings: [booking]);

        var result = await _sut.Execute(CreateRequest(document));

        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Contains(success.Value.Warnings, w =>
            w.Code == TakeoutWarningCode.MissingReferenceSkipped && w.Entity == nameof(Booking));
        Assert.Equal(1, success.Value.Bookings.Skipped);
        _bookings.Verify(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AdminClaimButCallerNotAdminInDatabase_WHEN_Execute_THEN_ReturnsForbiddenAndRollsBack(
        DateTime t0)
    {
        // Arrange
        _users.Setup(x => x.LockAdmins(It.IsAny<CancellationToken>())).ReturnsAsync(OtherUserIds);
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, "customer@example.test");
        var location = new TakeoutLocation(LocationSourceId, "Main Street");
        TakeoutDocument document = CreateDocument(t0, users: [customer], locations: [location]);

        // Act
        var result = await _sut.Execute(CreateRequest(document));

        // Assert
        var fault = Assert.IsType<Fault>(result.Value);
        Assert.Equal(Problem.Forbidden.Type, fault.Problem.Type);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _locations.Verify(x => x.Add(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
        _services.Verify(x => x.Add(It.IsAny<Service>(), It.IsAny<CancellationToken>()), Times.Never);
        _bookings.Verify(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _activityRecorder.VerifyNoOtherCalls();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_DocumentCreatesAdminEmployeeAndCustomer_WHEN_Execute_THEN_WarnsAndRecordsRoleChangeForPrivilegedOnly(
        DateTime t0)
    {
        // Arrange
        const int adminId = 201;
        const int employeeId = 202;
        const int customerId = 203;
        SetupAdminEmployeeCustomerAdds(adminId, employeeId, customerId);
        TakeoutDocument document = CreateAdminEmployeeCustomerDocument(t0);

        // Act
        var result = await _sut.Execute(CreateRequest(document));

        // Assert
        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Equal(2, success.Value.Warnings.Count(w => w.Code == TakeoutWarningCode.PrivilegedUserCreated));
        _activityRecorder.Verify(x => x.ChangeRole<User>(
            It.Is<ActivityQuery<User>>(q => q.UserId == CallerId && q.EntityId == adminId),
            It.IsAny<CancellationToken>()), Times.Once);
        _activityRecorder.Verify(x => x.ChangeRole<User>(
            It.Is<ActivityQuery<User>>(q => q.UserId == CallerId && q.EntityId == employeeId),
            It.IsAny<CancellationToken>()), Times.Once);
        _activityRecorder.Verify(x => x.ChangeRole<User>(It.IsAny<ActivityQuery<User>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        _activityRecorder.Verify(x => x.Import<TakeoutDocument>(It.IsAny<ActivityQuery<TakeoutDocument>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_DryRunDocumentCreatesPrivilegedUsers_WHEN_Execute_THEN_WarnsWithoutRecordingActivity(
        DateTime t0)
    {
        // Arrange
        SetupAdminEmployeeCustomerAdds(201, 202, 203);
        TakeoutDocument document = CreateAdminEmployeeCustomerDocument(t0);

        // Act
        var result = await _sut.Execute(CreateRequest(document, dryRun: true));

        // Assert
        var success = Assert.IsType<Success<ImportTakeoutResponse>>(result.Value);
        Assert.Equal(2, success.Value.Warnings.Count(w => w.Code == TakeoutWarningCode.PrivilegedUserCreated));
        _activityRecorder.Verify(x => x.ChangeRole<User>(It.IsAny<ActivityQuery<User>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _activityRecorder.Verify(x => x.Import<TakeoutDocument>(It.IsAny<ActivityQuery<TakeoutDocument>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CommitThrows_WHEN_Execute_THEN_ExceptionPropagatesWithoutRollback(
        DateTime t0,
        Exception testException)
    {
        // Arrange
        _transaction.Setup(x => x.Commit(It.IsAny<CancellationToken>())).ThrowsAsync(testException);
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, "customer@example.test");
        TakeoutDocument document = CreateDocument(t0, users: [customer]);

        // Act
        Exception thrown = await Assert.ThrowsAsync<Exception>(() => _sut.Execute(CreateRequest(document)));

        // Assert
        Assert.Same(testException, thrown);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Never);
        _activityRecorder.Verify(x => x.Import<TakeoutDocument>(It.IsAny<ActivityQuery<TakeoutDocument>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupAdminEmployeeCustomerAdds(int adminId, int employeeId, int customerId)
    {
        _users.Setup(x => x.Add(It.Is<User>(u => u.Email == MailOf("admin")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminId);
        _users.Setup(x => x.Add(It.Is<User>(u => u.Email == MailOf("employee")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);
        _users.Setup(x => x.Add(It.Is<User>(u => u.Email == MailOf("customer")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerId);
    }

    private static TakeoutDocument CreateAdminEmployeeCustomerDocument(DateTime t0)
    {
        var admin = new TakeoutUser(AdminSourceId, UserRole.Admin, MailOf("admin"), "Ann");
        var employee = new TakeoutUser(EmployeeSourceId, UserRole.Employee, MailOf("employee"), "Erin");
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, MailOf("customer"), "Carl");
        return CreateDocument(t0, users: [admin, employee, customer]);
    }

    private static ImportTakeoutRequest CreateRequest(TakeoutDocument document, bool dryRun = false,
        ClaimsPrincipal? user = null) =>
        new(document, dryRun)
        {
            User = user ?? CreateUser(CallerId, UserRole.Admin),
            TenantId = TestHelpers.CreateTenantContext().TenantId
        };

    private static TakeoutDocument CreateDocument(
        DateTime exportedAt,
        IReadOnlyList<TakeoutUser>? users = null,
        IReadOnlyList<TakeoutLocation>? locations = null,
        IReadOnlyList<TakeoutService>? services = null,
        IReadOnlyList<TakeoutBooking>? bookings = null,
        IReadOnlyList<TakeoutCalendarSlot>? calendar = null,
        int schemaVersion = TakeoutDocument.CurrentSchemaVersion) =>
        new(schemaVersion, exportedAt, TenantSlug, TakeoutScope.TenantWorkspace,
            users ?? [], locations ?? [], services ?? [], bookings ?? [], calendar ?? []);

    /// <summary>Staff member, customer, location, a service staffed by the employee, and a booking for the customer.</summary>
    private static TakeoutDocument CreateBookingDocument(DateTime t0, string employeeMail, string customerMail,
        IReadOnlyList<TakeoutCalendarSlot> calendar)
    {
        var employee = new TakeoutUser(EmployeeSourceId, UserRole.Employee, employeeMail, "Erin");
        var customer = new TakeoutUser(CustomerSourceId, UserRole.Customer, customerMail, "Carl");
        var location = new TakeoutLocation(LocationSourceId, "Main Street");
        var service = new TakeoutService(ServiceSourceId, "Haircut", SlotLength, Single(EmployeeSourceId));
        var booking = new TakeoutBooking(BookingSourceId, CustomerSourceId, ServiceSourceId, "Bring keys");
        return CreateDocument(t0, users: [employee, customer], locations: [location], services: [service],
            bookings: [booking], calendar: calendar);
    }

    private static List<int> Single(int id) => [id];

    // The command normalizes emails to lower case, so expectations must use the canonical form.
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Emails are lower case")]
    private static string MailOf(string local) => $"{local}@example.test".ToLowerInvariant();
}
