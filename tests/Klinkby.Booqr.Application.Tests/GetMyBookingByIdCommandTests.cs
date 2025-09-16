using System.Security.Claims;
using Klinkby.Booqr.Application.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace Klinkby.Booqr.Application.Tests;

public class GetMyBookingByIdCommandTests
{
    private readonly Mock<IMyBookingRepository> _repo = new();

    private static ClaimsPrincipal CreateUser(int id = 42, params string[] roles)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        return new ClaimsPrincipal(identity);
    }

    private GetMyBookingByIdCommand CreateSut() => new(
        _repo.Object,
        NullLogger<GetMyBookingByIdCommand>.Instance);

    [Fact]
    public async Task GIVEN_CustomerOwnsBooking_WHEN_Execute_THEN_ReturnsBooking()
    {
        // Arrange
        int userId = 42;
        var user = CreateUser(userId);
        var request = new GetMyBookingByIdRequest(userId, 1001) { User = user };
        var booking = new MyBooking(DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 10, 20, 30, userId, false) { Id = request.BookingId };
        _repo.Setup(x => x.GetById(request.BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(booking, result);
    }

    [Fact]
    public async Task GIVEN_CustomerNotOwner_WHEN_Execute_THEN_ThrowsUnauthorized_And_DoesNotQueryRepo()
    {
        // Arrange
        var user = CreateUser(42);
        var request = new GetMyBookingByIdRequest(99, 2002) { User = user }; // trying to read someone else's booking by user id
        var sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.Execute(request));
        _repo.Verify(x => x.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_Employee_WHEN_Execute_THEN_CanViewAnyBooking()
    {
        // Arrange
        var user = CreateUser(7, UserRole.Employee);
        var request = new GetMyBookingByIdRequest(123, 3003) { User = user };
        var booking = new MyBooking(DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 10, 20, 30, 999, true) { Id = request.BookingId };
        _repo.Setup(x => x.GetById(request.BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(booking, result);
    }

    [Fact]
    public async Task GIVEN_Admin_WHEN_Execute_THEN_CanViewAnyBooking()
    {
        // Arrange
        var user = CreateUser(8, UserRole.Admin);
        var request = new GetMyBookingByIdRequest(321, 4004) { User = user };
        var booking = new MyBooking(DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 11, 22, 33, 111, false) { Id = request.BookingId };
        _repo.Setup(x => x.GetById(request.BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(booking, result);
    }

    [Fact]
    public async Task GIVEN_BookingNotFound_WHEN_Execute_THEN_ReturnsNull()
    {
        // Arrange
        var user = CreateUser(42);
        var request = new GetMyBookingByIdRequest(42, 5005) { User = user };
        _repo.Setup(x => x.GetById(request.BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MyBooking?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.Null(result);
    }
}
