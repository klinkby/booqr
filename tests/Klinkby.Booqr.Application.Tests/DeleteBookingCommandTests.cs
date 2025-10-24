using System.Data;

namespace Klinkby.Booqr.Application.Tests;

public class DeleteBookingCommandTests
{
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<ICalendarRepository> _calendar = new();
    private readonly Mock<ITransaction> _transaction = new();


    private DeleteBookingCommand CreateSut()
    {
        return new DeleteBookingCommand(
            _bookings.Object,
            _calendar.Object,
            _transaction.Object,
            NullLogger<DeleteBookingCommand>.Instance,
            NullLogger<AddVacancyCommand>.Instance);
    }

    [Fact]
    public async Task GIVEN_BookingNotFound_WHEN_Execute_THEN_BeginsAndCommits_NoDeletes()
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(123) { User = CreateUser(42, UserRole.Employee) };
        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        DeleteBookingCommand sut = CreateSut();

        // Act
        await sut.Execute(request);

        // Assert
        _transaction.Verify(x => x.Begin(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Once);
        _bookings.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendar.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Unauthorized_NotEmployeeOrAdmin_EvenIfOwner_WHEN_Execute_THEN_Throws(DateTime t0, Booking autoBooking, CalendarEvent autoEvent)
    {
        // Arrange
        var userId = 42;
        var roles = Array.Empty<string>();
        ClaimsPrincipal user = CreateUser(userId, roles);
        var request = new AuthenticatedByIdRequest(321) { User = user };

        var booking = autoBooking with { CustomerId = userId, Id = request.Id };
        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
        var calendarEvent = autoEvent with { BookingId = request.Id, StartTime = t0, EndTime = t0.AddHours(1), Id = 777 };
        _calendar.Setup(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendarEvent);

        DeleteBookingCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.Execute(request));
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Unauthorized_EmployeeButNotOwner_WHEN_Execute_THEN_Throws(DateTime t0, Booking autoBooking, CalendarEvent autoEvent)
    {
        // Arrange
        var userId = 42;
        var roles = new[] { UserRole.Employee };
        ClaimsPrincipal user = CreateUser(userId, roles);
        var request = new AuthenticatedByIdRequest(321) { User = user };

        var booking = autoBooking with { CustomerId = 99, Id = request.Id };
        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
        var calendarEvent = autoEvent with { BookingId = request.Id, StartTime = t0, EndTime = t0.AddHours(1), Id = 777 };
        _calendar.Setup(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendarEvent);

        DeleteBookingCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.Execute(request));
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Authorized_WHEN_Execute_THEN_DeletesBookingAndReopensVacancy(DateTime t0, Booking autoBooking, CalendarEvent autoEvent)
    {
        // Arrange
        var userId = 42;
        ClaimsPrincipal user = CreateUser(userId, UserRole.Employee);
        var request = new AuthenticatedByIdRequest(555) { User = user };

        var booking = autoBooking with { CustomerId = userId, Notes = null, Id = request.Id };
        var calEvent = autoEvent with { BookingId = request.Id, StartTime = t0, EndTime = t0.AddHours(1), Id = 901 };

        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
        _calendar.Setup(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calEvent);
        _bookings.Setup(x => x.Delete(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // For reopening vacancy, AddVacancyCommand.AddVacancyCore will query range and then add a vacancy
        _calendar.Setup(x => x.GetRange(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IPageQuery>(), true, true,
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<CalendarEvent>());
        _calendar.Setup(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1001);

        DeleteBookingCommand sut = CreateSut();

        // Act
        await sut.Execute(request);

        // Assert
        _transaction.Verify(x => x.Begin(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.Delete(calEvent.Id, It.IsAny<CancellationToken>()), Times.Once);
        _bookings.Verify(x => x.Delete(request.Id, It.IsAny<CancellationToken>()), Times.Once);
        _calendar.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ExceptionDuringProcess_WHEN_Execute_THEN_RollsBack(Booking autoBooking)
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(777) { User = CreateUser(42, UserRole.Employee) };
        var booking = autoBooking with { CustomerId = request.AuthenticatedUserId, Id = request.Id };
        _bookings.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
        _calendar.Setup(x => x.GetByBookingId(request.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        DeleteBookingCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Execute(request));
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }
}
