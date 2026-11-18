namespace Klinkby.Booqr.Application.Tests.Commands;

public class GetBookingByIdCommandTests
{
    private readonly Mock<IBookingRepository> _bookings = new();

    private GetBookingByIdCommand CreateSut() =>
        new(_bookings.Object, NullLogger<GetBookingByIdCommand>.Instance);

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerOwnsBooking_WHEN_Execute_THEN_ReturnsBookingWithNotes(
        int userId, Booking autoBooking)
    {
        var booking = autoBooking with { CustomerId = userId };
        var request = new AuthenticatedByIdRequest(booking.Id) { User = CreateUser(userId) };
        _bookings.Setup(x => x.GetById(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var result = await CreateSut().Execute(request);

        var success = Assert.IsType<Success<Booking>>(result.Value);
        Assert.Equal(booking, success.Value);
        Assert.Equal(booking.Notes, success.Value.Notes);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerNotOwner_WHEN_Execute_THEN_ReturnsForbidden(
        int userId, Booking autoBooking)
    {
        var booking = autoBooking with { CustomerId = userId + 1 };
        var request = new AuthenticatedByIdRequest(booking.Id) { User = CreateUser(userId) };
        _bookings.Setup(x => x.GetById(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var result = await CreateSut().Execute(request);

        var fault = Assert.IsType<Fault>(result.Value);
        Assert.Equal(Problem.Forbidden.Type, fault.Problem.Type);
    }

    [Theory]
    [ApplicationAutoData]
    public Task GIVEN_Employee_WHEN_Execute_THEN_CanViewAnyBooking(int userId, Booking autoBooking) =>
        AssertStaffCanView(UserRole.Employee, userId, autoBooking);

    [Theory]
    [ApplicationAutoData]
    public Task GIVEN_Admin_WHEN_Execute_THEN_CanViewAnyBooking(int userId, Booking autoBooking) =>
        AssertStaffCanView(UserRole.Admin, userId, autoBooking);

    private async Task AssertStaffCanView(string role, int userId, Booking autoBooking)
    {
        var booking = autoBooking with { CustomerId = userId + 1 };
        var request = new AuthenticatedByIdRequest(booking.Id) { User = CreateUser(userId, role) };
        _bookings.Setup(x => x.GetById(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var result = await CreateSut().Execute(request);

        var success = Assert.IsType<Success<Booking>>(result.Value);
        Assert.Equal(booking, success.Value);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_BookingNotFound_WHEN_Execute_THEN_ReturnsNotFound(int userId, int bookingId)
    {
        var request = new AuthenticatedByIdRequest(bookingId) { User = CreateUser(userId, UserRole.Employee) };
        _bookings.Setup(x => x.GetById(bookingId, It.IsAny<CancellationToken>())).ReturnsAsync((Booking?)null);

        var result = await CreateSut().Execute(request);

        var fault = Assert.IsType<Fault>(result.Value);
        Assert.Equal(Problem.NotFound.Type, fault.Problem.Type);
    }
}
