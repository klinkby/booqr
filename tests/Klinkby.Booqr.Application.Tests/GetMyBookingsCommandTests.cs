using System.Security.Claims;
using Klinkby.Booqr.Application.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests;

public class GetMyBookingsCommandTests
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

    private static async IAsyncEnumerable<MyBooking> Yield(params MyBooking[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private GetMyBookingsCommand CreateSut(TimeProvider? timeProvider = null) => new(
        _repo.Object,
        timeProvider ?? new FakeTimeProvider(),
        NullLogger<GetMyBookingsCommand>.Instance);

    [Fact]
    public void GIVEN_CustomerOwnsProfile_WHEN_Execute_THEN_RepositoryCalled()
    {
        // Arrange
        int userId = 42;
        var user = CreateUser(userId);
        var request = new GetMyBookingsRequest(userId, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(2)) { User = user };

        _repo.Setup(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request, It.IsAny<CancellationToken>()))
            .Returns(Yield());

        var sut = CreateSut();

        // Act
        var _ = sut.Execute(request);

        // Assert
        _repo.Verify(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GIVEN_CustomerNotOwner_WHEN_Execute_THEN_ThrowsUnauthorized_And_DoesNotQueryRepo()
    {
        // Arrange
        var user = CreateUser(42);
        var request = new GetMyBookingsRequest(99, null, null) { User = user };
        var sut = CreateSut();

        // Act + Assert
        Assert.Throws<UnauthorizedAccessException>(() => sut.Execute(request));
        _repo.Verify(x => x.GetRangeByUserId(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GIVEN_Employee_WHEN_Execute_THEN_CanViewAnyUsersBookings()
    {
        // Arrange
        var user = CreateUser(7, UserRole.Employee);
        var request = new GetMyBookingsRequest(123, null, null) { User = user };

        _repo.Setup(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request, It.IsAny<CancellationToken>()))
            .Returns(Yield());

        var sut = CreateSut();

        // Act
        var _ = sut.Execute(request);

        // Assert
        _repo.Verify(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GIVEN_Admin_WHEN_Execute_THEN_CanViewAnyUsersBookings()
    {
        // Arrange
        var user = CreateUser(8, UserRole.Admin);
        var request = new GetMyBookingsRequest(321, null, null) { User = user };

        _repo.Setup(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request, It.IsAny<CancellationToken>()))
            .Returns(Yield());

        var sut = CreateSut();

        // Act
        var _ = sut.Execute(request);

        // Assert
        _repo.Verify(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GIVEN_NullFromAndTo_WHEN_Execute_THEN_DefaultsApplied()
    {
        // Arrange
        var t0 = new DateTimeOffset(new DateTime(2025, 01, 15, 10, 30, 0, DateTimeKind.Utc));
        var fakeTime = new FakeTimeProvider(t0);
        var user = CreateUser(77, UserRole.Employee);
        var request = new GetMyBookingsRequest(999, null, null) { User = user };

        _repo.Setup(x => x.GetRangeByUserId(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), request, It.IsAny<CancellationToken>()))
            .Returns(Yield());

        var sut = CreateSut(fakeTime);

        // Act
        var _ = sut.Execute(request);

        // Assert
        _repo.Verify(x => x.GetRangeByUserId(
            request.Id,
            t0.UtcDateTime.AddDays(-1),
            DateTime.MaxValue,
            request,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
