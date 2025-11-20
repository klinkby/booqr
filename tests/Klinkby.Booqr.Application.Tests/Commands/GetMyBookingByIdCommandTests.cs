namespace Klinkby.Booqr.Application.Tests;

public class GetMyBookingByIdCommandTests
{
    private readonly Mock<IMyBookingRepository> _repo = new();

    private GetMyBookingByIdCommand CreateSut()
    {
        return new GetMyBookingByIdCommand(
            _repo.Object,
            NullLogger<GetMyBookingByIdCommand>.Instance);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerOwnsBooking_WHEN_Execute_THEN_ReturnsBooking(DateTime t0, MyBooking autoBooking)
    {
        // Arrange
        var userId = 42;
        ClaimsPrincipal user = CreateUser(userId);
        var request = new GetMyBookingByIdRequest(userId, 1001) { User = user };
        var booking = autoBooking with { CustomerId = userId, StartTime = t0, EndTime = t0.AddHours(1), Id = request.BookingId };
        _repo.Setup(x => x.GetById(request.BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        GetMyBookingByIdCommand sut = CreateSut();

        // Act
        MyBooking? result = await sut.Execute(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(booking, result);
    }

    [Fact]
    public async Task GIVEN_CustomerNotOwner_WHEN_Execute_THEN_ThrowsUnauthorized_And_DoesNotQueryRepo()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser();
        var request = new GetMyBookingByIdRequest(99, 2002)
            { User = user }; // trying to read someone else's booking by user id
        GetMyBookingByIdCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.Execute(request));
        _repo.Verify(x => x.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Employee_WHEN_Execute_THEN_CanViewAnyBooking(DateTime t0, MyBooking autoBooking)
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(7, UserRole.Employee);
        var request = new GetMyBookingByIdRequest(123, 3003) { User = user };
        var booking = autoBooking with { CustomerId = 999, StartTime = t0, EndTime = t0.AddHours(1), Id = request.BookingId };
        _repo.Setup(x => x.GetById(request.BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        GetMyBookingByIdCommand sut = CreateSut();

        // Act
        MyBooking? result = await sut.Execute(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(booking, result);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Admin_WHEN_Execute_THEN_CanViewAnyBooking(DateTime t0, MyBooking autoBooking)
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(8, UserRole.Admin);
        var request = new GetMyBookingByIdRequest(321, 4004) { User = user };
        var booking = autoBooking with { CustomerId = 111, StartTime = t0, EndTime = t0.AddHours(1), Id = request.BookingId };
        _repo.Setup(x => x.GetById(request.BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        GetMyBookingByIdCommand sut = CreateSut();

        // Act
        MyBooking? result = await sut.Execute(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(booking, result);
    }

    [Fact]
    public async Task GIVEN_BookingNotFound_WHEN_Execute_THEN_ReturnsNull()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser();
        var request = new GetMyBookingByIdRequest(42, 5005) { User = user };
        _repo.Setup(x => x.GetById(request.BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MyBooking?)null);

        GetMyBookingByIdCommand sut = CreateSut();

        // Act
        MyBooking? result = await sut.Execute(request);

        // Assert
        Assert.Null(result);
    }
}
