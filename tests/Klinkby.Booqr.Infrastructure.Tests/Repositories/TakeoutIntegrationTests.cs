using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Klinkby.Booqr.Application;
using Klinkby.Booqr.Application.Commands.Takeout;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public sealed class TakeoutIntegrationTests(ServiceProviderFixture serviceProvider)
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
    private static readonly PageQuery All = new(0, null);

    private readonly IUserRepository _users = serviceProvider.Services.GetRequiredService<IUserRepository>();
    private readonly ILocationRepository _locations = serviceProvider.Services.GetRequiredService<ILocationRepository>();
    private readonly IServiceRepository _services = serviceProvider.Services.GetRequiredService<IServiceRepository>();
    private readonly IEmployeeServiceRepository _employeeServices =
        serviceProvider.Services.GetRequiredService<IEmployeeServiceRepository>();
    private readonly IBookingRepository _bookings = serviceProvider.Services.GetRequiredService<IBookingRepository>();
    private readonly ICalendarRepository _calendar = serviceProvider.Services.GetRequiredService<ICalendarRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();
    private readonly ITenantContext _tenant = serviceProvider.Services.GetRequiredService<ITenantContext>();
    private readonly TimeProvider _time = serviceProvider.Services.GetRequiredService<TimeProvider>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_SeededTenant_WHEN_ExportThenDryRunImport_THEN_JsonIsValidAndNothingPersists(
        DateTime t0, User admin, User employee, User customer, Location location, Service service, Booking booking,
        string notes)
    {
        string adminEmail = $"{Guid.NewGuid():N}@example.test";
        string employeeEmail = $"{Guid.NewGuid():N}@example.test";
        string customerEmail = $"{Guid.NewGuid():N}@example.test";
        string passwordHash = $"{Guid.NewGuid():N}{Guid.NewGuid():N}"[..60];

        int adminId;
        int employeeId;
        int customerId;
        int locationId;
        int serviceId;
        int bookingId;
        int bookedSlotId;
        int vacancyId;
        int pastSlotId;
        string json;

        await _transaction.Begin();
        try
        {
            adminId = await _users.Add(admin with { Email = adminEmail, Role = UserRole.Admin });
            employeeId = await _users.Add(employee with
            {
                Email = employeeEmail, Role = UserRole.Employee, PasswordHash = passwordHash
            });
            customerId = await _users.Add(customer with { Email = customerEmail, Role = UserRole.Customer });
            locationId = await _locations.Add(location);
            serviceId = await _services.Add(service with { Employees = [] });
            await _employeeServices.Assign(serviceId, [employeeId]);
            bookingId = await _bookings.Add(booking with
            {
                CustomerId = customerId, ServiceId = serviceId, Notes = notes
            });
            bookedSlotId = await _calendar.Add(new CalendarEvent(employeeId, locationId, bookingId,
                t0.AddDays(1), t0.AddDays(1).AddHours(1)));
            vacancyId = await _calendar.Add(new CalendarEvent(employeeId, locationId, null,
                t0.AddDays(2), t0.AddDays(2).AddHours(1)));
            pastSlotId = await _calendar.Add(new CalendarEvent(employeeId, locationId, null,
                t0.AddDays(-2), t0.AddDays(-2).AddHours(1)));

            ExportTakeoutCommand export = new(_users, _locations, _services, _bookings, _calendar, _tenant, _time,
                NullLogger<ExportTakeoutCommand>.Instance);
            Result<TakeoutDocument> exported = await export.Execute(
                new ExportTakeoutRequest(TakeoutScope.TenantWorkspace) { User = AdminPrincipal(adminId) });

            TakeoutDocument document = Assert.IsType<Result<TakeoutDocument>.Success>(exported).Value;
            json = JsonSerializer.Serialize(document, WebOptions);
        }
        finally
        {
            await _transaction.Rollback();
        }

        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement root = parsed.RootElement;
        Assert.Equal(TakeoutDocument.DefaultSchemaUri, root.GetProperty("$schema").GetString());
        Assert.Equal(TakeoutDocument.CurrentSchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(nameof(TakeoutScope.TenantWorkspace), root.GetProperty("scope").GetString());
        Assert.Equal(_tenant.Slug, root.GetProperty("tenantSlug").GetString());

        string[] exportedEmails = root.GetProperty("users").EnumerateArray()
            .Select(u => u.GetProperty("email").GetString() ?? string.Empty)
            .ToArray();
        Assert.Contains(adminEmail, exportedEmails);
        Assert.Contains(employeeEmail, exportedEmails);
        Assert.Contains(customerEmail, exportedEmails);
        Assert.DoesNotContain(passwordHash, json, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);

        int[] calendarIds = Ids(root.GetProperty("calendar"), "id");
        Assert.Contains(bookedSlotId, calendarIds);
        Assert.Contains(vacancyId, calendarIds);
        Assert.DoesNotContain(pastSlotId, calendarIds);

        Assert.Contains(bookingId, Ids(root.GetProperty("bookings"), "id"));

        JsonElement exportedService = root.GetProperty("services").EnumerateArray()
            .Single(s => s.GetProperty("id").GetInt32() == serviceId);
        int[] serviceEmployeeIds = exportedService.GetProperty("employeeIds").EnumerateArray()
            .Select(e => e.GetInt32())
            .ToArray();
        Assert.Contains(employeeId, serviceEmployeeIds);

        int[] rowCountsBefore = await CountRows();

        TakeoutDocument? parsedDocument = JsonSerializer.Deserialize<TakeoutDocument>(json, WebOptions);
        Assert.NotNull(parsedDocument);
        ImportTakeoutCommand import = new(_users, _locations, _services, _employeeServices, _bookings, _calendar,
            _transaction, new Mock<IActivityRecorder>().Object, _time, NullLogger<ImportTakeoutCommand>.Instance);
        Result<ImportTakeoutResponse> imported = await import.Execute(
            new ImportTakeoutRequest(parsedDocument, ValidateOnly: true) { User = AdminPrincipal(adminId) });

        ImportTakeoutResponse response = Assert.IsType<Result<ImportTakeoutResponse>.Success>(imported).Value;
        Assert.True(response.ValidateOnly);
        Assert.True(response.Users.Created >= 3);
        Assert.True(response.Locations.Created >= 1);
        Assert.True(response.Services.Created >= 1);
        Assert.True(response.Bookings.Created >= 1);
        Assert.True(response.Calendar.Created >= 2);
        Assert.Contains(response.Warnings, w => w.Code == TakeoutWarningCode.UserCreatedWithoutPassword);

        HashSet<(string Entity, int Id)> seeded =
        [
            (nameof(User), adminId),
            (nameof(User), employeeId),
            (nameof(User), customerId),
            (nameof(Location), locationId),
            (nameof(Service), serviceId),
            (nameof(Booking), bookingId),
            (nameof(CalendarEvent), bookedSlotId),
            (nameof(CalendarEvent), vacancyId),
        ];
        TakeoutWarning[] seededProblems = response.Warnings
            .Where(w => w.Code is TakeoutWarningCode.CalendarOverlapSkipped
                or TakeoutWarningCode.MissingReferenceSkipped)
            .Where(w => w.SourceId is int id && seeded.Contains((w.Entity, id)))
            .ToArray();
        Assert.Empty(seededProblems);

        int[] rowCountsAfter = await CountRows();
        Assert.Equal(rowCountsBefore, rowCountsAfter);
    }

    private async Task<int[]> CountRows() =>
    [
        await _users.GetAll(All).CountAsync(),
        await _locations.GetAll(All).CountAsync(),
        await _services.GetAll(All).CountAsync(),
        await _bookings.GetAll(All).CountAsync(),
        await _calendar.GetAll(All).CountAsync(),
    ];

    private static int[] Ids(JsonElement array, string property) =>
        array.EnumerateArray().Select(e => e.GetProperty(property).GetInt32()).ToArray();

    private static ClaimsPrincipal AdminPrincipal(int userId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Role, UserRole.Admin),
        ], "test"));
}
