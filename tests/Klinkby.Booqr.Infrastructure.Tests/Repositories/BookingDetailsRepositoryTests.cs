namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public sealed class BookingDetailsRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IBookingRepository _bookings = serviceProvider.Services.GetRequiredService<IBookingRepository>();
    private readonly ICalendarRepository _calendar = serviceProvider.Services.GetRequiredService<ICalendarRepository>();
    private readonly ILocationRepository _locations = serviceProvider.Services.GetRequiredService<ILocationRepository>();
    private readonly IServiceRepository _services = serviceProvider.Services.GetRequiredService<IServiceRepository>();
    private readonly IBookingDetailsRepository _sut = serviceProvider.Services.GetRequiredService<IBookingDetailsRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();
    private readonly IUserRepository _users = serviceProvider.Services.GetRequiredService<IUserRepository>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_BookingDetails_WHEN_Seeded_THEN_CanBeReadBack(
        CalendarEvent vacancy, Location location, Service service, User customer, User employee, Booking booking)
    {
        await _transaction.Begin();
        List<BookingDetails> actuals;
        try
        {
            // Arrange: create dependent entities
            int customerId = await _users.Add(customer with { Role = UserRole.Customer });
            int serviceId = await _services.Add(service);
            booking = booking with { CustomerId = customerId, ServiceId = serviceId };
            int bookingId = await _bookings.Add(booking);

            int employeeId = await _users.Add(employee with { Role = UserRole.Employee });
            int locationId = await _locations.Add(location);
            vacancy = vacancy with { EmployeeId = employeeId, LocationId = locationId, BookingId = bookingId };
            await _calendar.Add(vacancy);

            // Act
            actuals = await _sut.GetRange(DateTime.MinValue, DateTime.MaxValue, new PageQuery())
                .ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        // Assert: find the booking details for our booking id
        BookingDetails? actual = actuals.SingleOrDefault(x => x.Id == vacancy.BookingId);
        Assert.NotNull(actual);
        Assert.Equal(vacancy.StartTime, actual!.StartTime);
        Assert.Equal(service.Name, actual.Service);
        Assert.Equal(service.Duration, actual.Duration);
        Assert.Equal(location.Name, actual.Location);
        Assert.Equal(employee.Name, actual.Employee);
        Assert.Equal(customer.Name, actual.CustomerName);
        Assert.Equal(customer.Email, actual.CustomerEmail);
    }
}
