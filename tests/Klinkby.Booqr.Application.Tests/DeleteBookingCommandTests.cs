using System.Security.Claims;
using Klinkby.Booqr.Application.Bookings;
using Klinkby.Booqr.Application.Vacancies;
using Microsoft.Extensions.Logging.Abstractions;
using static Klinkby.Booqr.Application.Tests.TestHelpers;

namespace Klinkby.Booqr.Application.Tests;

public class DeleteBookingCommandTests
{
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<ICalendarRepository> _calendar = new();
    private readonly Mock<ITransaction> _transaction = new();


    private DeleteBookingCommand CreateSut() => new(
        _bookings.Object,
        _calendar.Object,
        _transaction.Object,
        NullLogger<DeleteBookingCommand>.Instance,
        NullLogger<AddVacancyCommand>.Instance);

    [Fact]
    public async Task GIVEN_BookingNotFound_WHEN_Execute_THEN_BeginsAndCommits_NoDeletes()
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(123) { User = CreateUser(42, UserRole.Employee) };
        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var sut = CreateSut();

        // Act
        await sut.Execute(request);

        // Assert
        _transaction.Verify(x => x.Begin(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Once);
        _bookings.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendar.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, true)] // not employee/admin, even if owner
    [InlineData(true, false)] // employee/admin but not owner
    public async Task GIVEN_Unauthorized_WHEN_Execute_THEN_Throws(bool isEmployee, bool isOwner)
    {
        // Arrange
        int userId = 42;
        var roles = isEmployee ? new[] { UserRole.Employee } : Array.Empty<string>();
        var user = CreateUser(userId, roles);
        var request = new AuthenticatedByIdRequest(321) { User = user };

        var booking = new Booking(isOwner ? userId : 99, 10, "notes") { Id = request.Id };
        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
        _calendar.Setup(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalendarEvent(7, 3, request.Id, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)) { Id = 777 });

        var sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.Execute(request));
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_Authorized_WHEN_Execute_THEN_DeletesBookingAndReopensVacancy()
    {
        // Arrange
        int userId = 42;
        var user = CreateUser(userId, UserRole.Employee);
        var request = new AuthenticatedByIdRequest(555) { User = user };

        var booking = new Booking(userId, 10, null) { Id = request.Id };
        var calEvent = new CalendarEvent(7, 3, request.Id, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)) { Id = 901 };

        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
        _calendar.Setup(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calEvent);
        _bookings.Setup(x => x.Delete(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // For reopening vacancy, AddVacancyCommand.AddVacancyCore will query range and then add a vacancy
        _calendar.Setup(x => x.GetRange(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IPageQuery>(), true, true, It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<CalendarEvent>());
        _calendar.Setup(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1001);

        var sut = CreateSut();

        // Act
        await sut.Execute(request);

        // Assert
        _transaction.Verify(x => x.Begin(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.Delete(calEvent.Id, It.IsAny<CancellationToken>()), Times.Once);
        _bookings.Verify(x => x.Delete(request.Id, It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ExceptionDuringProcess_WHEN_Execute_THEN_RollsBack()
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(777) { User = CreateUser(42, UserRole.Employee) };
        var booking = new Booking(request.AuthenticatedUserId, 10, null) { Id = request.Id };
        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
        _calendar.Setup(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Execute(request));
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }
}
