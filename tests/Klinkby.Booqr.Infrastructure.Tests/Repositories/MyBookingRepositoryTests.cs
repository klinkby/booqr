namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public class MyBookingRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IBookingRepository _bookings = serviceProvider.Services.GetRequiredService<IBookingRepository>();
    private readonly ICalendarRepository _calendar = serviceProvider.Services.GetRequiredService<ICalendarRepository>();
    private readonly ILocationRepository _location = serviceProvider.Services.GetRequiredService<ILocationRepository>();
    private readonly IServiceRepository _services = serviceProvider.Services.GetRequiredService<IServiceRepository>();
    private readonly IMyBookingRepository _sut = serviceProvider.Services.GetRequiredService<IMyBookingRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();
    private readonly IUserRepository _users = serviceProvider.Services.GetRequiredService<IUserRepository>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_MyBooking_THEN_CanBeReadBack(
        CalendarEvent vacancy, Location location, Service service, User customer, User employee, Booking booking)
    {
        await _transaction.Begin();
        MyBooking? actual1;
        List<MyBooking> actuals1;
        try
        {
            booking = booking with
            {
                CustomerId = await _users.Add(customer with { Role = UserRole.Customer }),
                ServiceId = await _services.Add(service)
            };
            booking = booking with { Id = await _bookings.Add(booking) };
            vacancy = vacancy with
            {
                EmployeeId = await _users.Add(employee with { Role = UserRole.Employee }),
                LocationId = await _location.Add(location),
                BookingId = await _bookings.Add(booking)
            };
            await _calendar.Add(vacancy);

            actual1 = await _sut.GetById(vacancy.BookingId.Value);
            actuals1 = await _sut
                .GetRangeByUserId(booking.CustomerId, DateTime.MinValue, DateTime.MaxValue, new PageQuery())
                .ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Equal(vacancy.BookingId.Value, actual1!.Id);
        Assert.Contains(vacancy.BookingId.Value, actuals1.Select(x => x.Id));
    }
}
