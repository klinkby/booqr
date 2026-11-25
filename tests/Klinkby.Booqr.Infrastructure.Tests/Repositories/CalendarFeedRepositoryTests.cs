namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public class CalendarFeedRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IBookingRepository _bookings = serviceProvider.Services.GetRequiredService<IBookingRepository>();
    private readonly ICalendarRepository _calendar = serviceProvider.Services.GetRequiredService<ICalendarRepository>();
    private readonly ICalendarFeedRepository _sut = serviceProvider.Services.GetRequiredService<ICalendarFeedRepository>();
    private readonly ILocationRepository _location = serviceProvider.Services.GetRequiredService<ILocationRepository>();
    private readonly IServiceRepository _services = serviceProvider.Services.GetRequiredService<IServiceRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();
    private readonly IUserRepository _users = serviceProvider.Services.GetRequiredService<IUserRepository>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_BookingWhereUserIsCustomer_WHEN_GetEvents_THEN_ReturnsEventWithServiceAndLocation(
        DateTime t0, CalendarEvent slot, Location location, Service service, User customer, User employee,
        Booking booking)
    {
        await _transaction.Begin();
        IReadOnlyList<CalendarFeedEvent> actuals;
        int customerId, bookingId;
        DateTime startTime = t0.AddHours(9), endTime = t0.AddHours(10);
        try
        {
            customerId = await _users.Add(customer with { Role = UserRole.Customer });
            booking = booking with
            {
                CustomerId = customerId,
                ServiceId = await _services.Add(service with { Name = "Haircut" })
            };
            bookingId = await _bookings.Add(booking);
            slot = slot with
            {
                EmployeeId = await _users.Add(employee with { Role = UserRole.Employee }),
                LocationId = await _location.Add(location with
                {
                    Name = "Main Studio", Address1 = "Vestergade 1", Address2 = "Floor 2", City = "Copenhagen"
                }),
                BookingId = bookingId,
                StartTime = startTime,
                EndTime = endTime
            };
            await _calendar.Add(slot);

            actuals = await _sut.GetEvents(customerId, t0, t0.AddDays(1), 1000).ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        CalendarFeedEvent actual = Assert.Single(actuals, x => x.Id == bookingId);
        Assert.Equal(DateTimeKind.Utc, actual.StartTime.Kind);
        Assert.Equal(DateTimeKind.Utc, actual.EndTime.Kind);
        Assert.Equal(startTime, actual.StartTime);
        Assert.Equal(endTime, actual.EndTime);
        Assert.Equal("Haircut", actual.Service);
        Assert.Equal("Main Studio", actual.Location);
        Assert.Equal("Vestergade 1", actual.Address1);
        Assert.Equal("Floor 2", actual.Address2);
        Assert.Equal("2301", actual.Zip);
        Assert.Equal("Copenhagen", actual.City);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_BookingWhereUserIsEmployee_WHEN_GetEvents_THEN_ReturnsEvent(
        DateTime t0, CalendarEvent slot, Location location, Service service, User customer, User employee,
        Booking booking)
    {
        await _transaction.Begin();
        IReadOnlyList<CalendarFeedEvent> actuals;
        int employeeId, bookingId;
        try
        {
            booking = booking with
            {
                CustomerId = await _users.Add(customer with { Role = UserRole.Customer }),
                ServiceId = await _services.Add(service)
            };
            bookingId = await _bookings.Add(booking);
            employeeId = await _users.Add(employee with { Role = UserRole.Employee });
            slot = slot with
            {
                EmployeeId = employeeId,
                LocationId = await _location.Add(location),
                BookingId = bookingId,
                StartTime = t0.AddHours(9),
                EndTime = t0.AddHours(10)
            };
            await _calendar.Add(slot);

            actuals = await _sut.GetEvents(employeeId, t0, t0.AddDays(1), 1000).ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Contains(actuals, x => x.Id == bookingId);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_DeletedBooking_WHEN_GetEvents_THEN_Excludes(
        DateTime t0, CalendarEvent slot, Location location, Service service, User customer, User employee,
        Booking booking)
    {
        await _transaction.Begin();
        IReadOnlyList<CalendarFeedEvent> actuals;
        int customerId, bookingId;
        try
        {
            customerId = await _users.Add(customer with { Role = UserRole.Customer });
            booking = booking with
            {
                CustomerId = customerId,
                ServiceId = await _services.Add(service)
            };
            bookingId = await _bookings.Add(booking);
            slot = slot with
            {
                EmployeeId = await _users.Add(employee with { Role = UserRole.Employee }),
                LocationId = await _location.Add(location),
                BookingId = bookingId,
                StartTime = t0.AddHours(9),
                EndTime = t0.AddHours(10)
            };
            await _calendar.Add(slot);
            await _bookings.Delete(bookingId, CancellationToken.None);

            actuals = await _sut.GetEvents(customerId, t0, t0.AddDays(1), 1000).ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.DoesNotContain(actuals, x => x.Id == bookingId);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_BookingOutsideRange_WHEN_GetEvents_THEN_Excludes(
        DateTime t0, CalendarEvent slot, Location location, Service service, User customer, User employee,
        Booking booking)
    {
        await _transaction.Begin();
        IReadOnlyList<CalendarFeedEvent> actuals;
        int customerId, bookingId;
        try
        {
            customerId = await _users.Add(customer with { Role = UserRole.Customer });
            booking = booking with
            {
                CustomerId = customerId,
                ServiceId = await _services.Add(service)
            };
            bookingId = await _bookings.Add(booking);
            slot = slot with
            {
                EmployeeId = await _users.Add(employee with { Role = UserRole.Employee }),
                LocationId = await _location.Add(location),
                BookingId = bookingId,
                StartTime = t0.AddDays(2),
                EndTime = t0.AddDays(2).AddHours(1)
            };
            await _calendar.Add(slot);

            actuals = await _sut.GetEvents(customerId, t0, t0.AddDays(1), 1000).ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.DoesNotContain(actuals, x => x.Id == bookingId);
    }
}
