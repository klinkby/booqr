using Klinkby.Booqr.Application.Vacancies;
using Microsoft.Extensions.Logging.Abstractions;

namespace Klinkby.Booqr.Application.Tests;

public class AddVacancyCommandTests
{
    private readonly static DateTime StartTime = TestHelpers.TimeProvider.GetUtcNow().UtcDateTime;

    private readonly AddVacancyRequest _query = new(
        42, 1, StartTime, StartTime.AddHours(1))
    {
        User = ApplicationAutoDataAttribute.GetTestUser()
    };

    private readonly Mock<ICalendarRepository> _repoMock = new();
    private readonly Mock<ITransaction> _transactionMock = new();

    [Theory]
    [InlineData(666, 1)]
    [InlineData(null, 2)]
    public async Task GIVEN_Conflicts_WHEN_AddCalendarEvent_THEN_Throws(int? bookingId, int locationId)
    {
        List<CalendarEvent> events =
        [
            new(_query.EmployeeId ?? 0, _query.LocationId, null, _query.StartTime, _query.EndTime),
            new(_query.EmployeeId ?? 0, locationId, bookingId, _query.StartTime, _query.EndTime)
        ];
        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object,
            NullLogger<AddVacancyCommand>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AddCalendarEvent(_query, events, 0, CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_CompletelyCovered_WHEN_AddCalendarEvent_THEN_ReturnsExistingId()
    {
        // Arrange
        CalendarEvent coveringEvent = CreateCalendarEvent(100, _query.EmployeeId, _query.LocationId, null,
            _query.StartTime.AddHours(-1), _query.EndTime.AddHours(1));
        List<CalendarEvent> events = [coveringEvent];

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object,
            NullLogger<AddVacancyCommand>.Instance);

        // Act
        var result = await sut.AddCalendarEvent(_query, events, _query.EmployeeId ?? 0, CancellationToken.None);

        // Assert
        Assert.Equal(coveringEvent.Id, result);
        _repoMock.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(x => x.Update(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CompletelyOverlapped_WHEN_AddCalendarEvent_THEN_DeletesOverlappedAndCreatesNew()
    {
        // Arrange
        CalendarEvent overlapped1 = CreateCalendarEvent(101, _query.EmployeeId, _query.LocationId, null,
            _query.StartTime.AddMinutes(15), _query.StartTime.AddMinutes(30));
        CalendarEvent overlapped2 = CreateCalendarEvent(102, _query.EmployeeId, _query.LocationId, null,
            _query.StartTime.AddMinutes(35), _query.StartTime.AddMinutes(50));
        List<CalendarEvent> events = [overlapped1, overlapped2];

        _repoMock.Setup(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(999);

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object,
            NullLogger<AddVacancyCommand>.Instance);

        // Act
        var result = await sut.AddCalendarEvent(_query, events, _query.EmployeeId ?? 0, CancellationToken.None);

        // Assert
        Assert.Equal(999, result);
        _repoMock.Verify(x => x.Delete(overlapped1.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(x => x.Delete(overlapped2.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_TwoIntersectingEvents_WHEN_AddCalendarEvent_THEN_CombinesIntoOne()
    {
        // Arrange - request spans 10:00-11:00, endOf ends at 10:00, startOf starts at 11:00
        CalendarEvent endOfEvent = CreateCalendarEvent(201, _query.EmployeeId, _query.LocationId, null,
            _query.StartTime.AddHours(-1), _query.StartTime);
        CalendarEvent startOfEvent = CreateCalendarEvent(202, _query.EmployeeId, _query.LocationId, null,
            _query.EndTime, _query.EndTime.AddHours(1));
        List<CalendarEvent> events = [endOfEvent, startOfEvent];

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object,
            NullLogger<AddVacancyCommand>.Instance);

        // Act
        var result = await sut.AddCalendarEvent(_query, events, _query.EmployeeId ?? 0, CancellationToken.None);

        // Assert
        Assert.Equal(endOfEvent.Id, result);
        _repoMock.Verify(x => x.Delete(startOfEvent.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(x => x.Update(It.Is<CalendarEvent>(e =>
            e.Id == endOfEvent.Id &&
            e.StartTime == endOfEvent.StartTime &&
            e.EndTime == startOfEvent.EndTime), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_IntersectsEndOf_WHEN_AddCalendarEvent_THEN_ExtendsEndOfEvent()
    {
        // Arrange - request spans 10:00-11:00, existing event ends at 10:00
        CalendarEvent endOfEvent = CreateCalendarEvent(301, _query.EmployeeId, _query.LocationId, null,
            _query.StartTime.AddHours(-1), _query.StartTime);
        List<CalendarEvent> events = [endOfEvent];

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object,
            NullLogger<AddVacancyCommand>.Instance);

        // Act
        var result = await sut.AddCalendarEvent(_query, events, _query.EmployeeId ?? 0, CancellationToken.None);

        // Assert
        Assert.Equal(endOfEvent.Id, result);
        _repoMock.Verify(x => x.Update(It.Is<CalendarEvent>(e =>
            e.Id == endOfEvent.Id &&
            e.StartTime == endOfEvent.StartTime &&
            e.EndTime == _query.EndTime), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_IntersectsStartOf_WHEN_AddCalendarEvent_THEN_ExtendsStartOfEvent()
    {
        // Arrange - request spans 10:00-11:00, existing event starts at 11:00
        CalendarEvent startOfEvent = CreateCalendarEvent(401, _query.EmployeeId, _query.LocationId, null,
            _query.EndTime, _query.EndTime.AddHours(1));
        List<CalendarEvent> events = [startOfEvent];

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object,
            NullLogger<AddVacancyCommand>.Instance);

        // Act
        var result = await sut.AddCalendarEvent(_query, events, _query.EmployeeId ?? 0, CancellationToken.None);

        // Assert
        Assert.Equal(startOfEvent.Id, result);
        _repoMock.Verify(x => x.Update(It.Is<CalendarEvent>(e =>
            e.Id == startOfEvent.Id &&
            e.StartTime == _query.StartTime &&
            e.EndTime == startOfEvent.EndTime), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_NoConflictsOrIntersections_WHEN_AddCalendarEvent_THEN_CreatesNewVacancy()
    {
        // Arrange
        List<CalendarEvent> events = [];

        _repoMock.Setup(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(555);

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object,
            NullLogger<AddVacancyCommand>.Instance);

        // Act
        var result = await sut.AddCalendarEvent(_query, events, _query.EmployeeId ?? 0, CancellationToken.None);

        // Assert
        Assert.Equal(555, result);
        _repoMock.Verify(x => x.Add(It.Is<CalendarEvent>(e =>
            e.EmployeeId == _query.EmployeeId &&
            e.LocationId == _query.LocationId &&
            e.StartTime == _query.StartTime &&
            e.EndTime == _query.EndTime &&
            e.BookingId == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CalendarEvent CreateCalendarEvent(int id, int? employeeId, int locationId, int? bookingId,
        DateTime startTime, DateTime endTime)
    {
        return new CalendarEvent(employeeId ?? 0, locationId, bookingId, startTime, endTime) { Id = id };
    }
}
