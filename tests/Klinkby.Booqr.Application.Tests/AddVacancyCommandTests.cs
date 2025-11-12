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
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Conflicts_BookingConflict_WHEN_AddCalendarEvent_THEN_Throws(CalendarEvent e1, CalendarEvent e2)
    {
        int? bookingId = 666;
        int locationId = 1;
        List<CalendarEvent> events =
        [
            e1 with { EmployeeId = _query.EmployeeId ?? 0, LocationId = _query.LocationId, BookingId = null, StartTime = _query.StartTime, EndTime = _query.EndTime },
            e2 with { EmployeeId = _query.EmployeeId ?? 0, LocationId = locationId, BookingId = bookingId, StartTime = _query.StartTime, EndTime = _query.EndTime }
        ];
        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object, _activityRecorder.Object,
            NullLogger<AddVacancyCommand>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AddCalendarEvent(_query, events, 0, CancellationToken.None));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Conflicts_LocationConflict_WHEN_AddCalendarEvent_THEN_Throws(CalendarEvent e1, CalendarEvent e2)
    {
        int? bookingId = null;
        int locationId = 2;
        List<CalendarEvent> events =
        [
            e1 with { EmployeeId = _query.EmployeeId ?? 0, LocationId = _query.LocationId, BookingId = null, StartTime = _query.StartTime, EndTime = _query.EndTime },
            e2 with { EmployeeId = _query.EmployeeId ?? 0, LocationId = locationId, BookingId = bookingId, StartTime = _query.StartTime, EndTime = _query.EndTime }
        ];
        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object, _activityRecorder.Object,
            NullLogger<AddVacancyCommand>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AddCalendarEvent(_query, events, 0, CancellationToken.None));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CompletelyCovered_WHEN_AddCalendarEvent_THEN_ReturnsExistingId(CalendarEvent coveringEvent)
    {
        // Arrange
        coveringEvent = coveringEvent with
        {
            Id = 100,
            EmployeeId = _query.EmployeeId ?? 0,
            LocationId = _query.LocationId,
            BookingId = null,
            StartTime = _query.StartTime.AddHours(-1),
            EndTime = _query.EndTime.AddHours(1)
        };
        List<CalendarEvent> events = [coveringEvent];

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object, _activityRecorder.Object,
            NullLogger<AddVacancyCommand>.Instance);

        // Act
        var result = await sut.AddCalendarEvent(_query, events, _query.EmployeeId ?? 0, CancellationToken.None);

        // Assert
        Assert.Equal(coveringEvent.Id, result);
        _repoMock.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(x => x.Update(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CompletelyOverlapped_WHEN_AddCalendarEvent_THEN_DeletesOverlappedAndCreatesNew(CalendarEvent overlapped1, CalendarEvent overlapped2)
    {
        // Arrange
        overlapped1 = overlapped1 with { Id = 101, EmployeeId = _query.EmployeeId ?? 0, LocationId = _query.LocationId, BookingId = null, StartTime = _query.StartTime.AddMinutes(15), EndTime = _query.StartTime.AddMinutes(30) };
        overlapped2 = overlapped2 with { Id = 102, EmployeeId = _query.EmployeeId ?? 0, LocationId = _query.LocationId, BookingId = null, StartTime = _query.StartTime.AddMinutes(35), EndTime = _query.StartTime.AddMinutes(50) };
        List<CalendarEvent> events = [overlapped1, overlapped2];

        _repoMock.Setup(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(999);

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object, _activityRecorder.Object,
            NullLogger<AddVacancyCommand>.Instance);

        // Act
        var result = await sut.AddCalendarEvent(_query, events, _query.EmployeeId ?? 0, CancellationToken.None);

        // Assert
        Assert.Equal(999, result);
        _repoMock.Verify(x => x.Delete(overlapped1.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(x => x.Delete(overlapped2.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(x => x.Add(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_TwoIntersectingEvents_WHEN_AddCalendarEvent_THEN_CombinesIntoOne(CalendarEvent endOfEvent, CalendarEvent startOfEvent)
    {
        // Arrange - request spans 10:00-11:00, endOf ends at 10:00, startOf starts at 11:00
        endOfEvent = endOfEvent with { Id = 201, EmployeeId = _query.EmployeeId ?? 0, LocationId = _query.LocationId, BookingId = null, StartTime = _query.StartTime.AddHours(-1), EndTime = _query.StartTime };
        startOfEvent = startOfEvent with { Id = 202, EmployeeId = _query.EmployeeId ?? 0, LocationId = _query.LocationId, BookingId = null, StartTime = _query.EndTime, EndTime = _query.EndTime.AddHours(1) };
        List<CalendarEvent> events = [endOfEvent, startOfEvent];

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object, _activityRecorder.Object,
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

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_IntersectsEndOf_WHEN_AddCalendarEvent_THEN_ExtendsEndOfEvent(CalendarEvent endOfEvent)
    {
        // Arrange - request spans 10:00-11:00, existing event ends at 10:00
        endOfEvent = endOfEvent with { Id = 301, EmployeeId = _query.EmployeeId ?? 0, LocationId = _query.LocationId, BookingId = null, StartTime = _query.StartTime.AddHours(-1), EndTime = _query.StartTime };
        List<CalendarEvent> events = [endOfEvent];

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object, _activityRecorder.Object,
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

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_IntersectsStartOf_WHEN_AddCalendarEvent_THEN_ExtendsStartOfEvent(CalendarEvent startOfEvent)
    {
        // Arrange - request spans 10:00-11:00, existing event starts at 11:00
        startOfEvent = startOfEvent with { Id = 401, EmployeeId = _query.EmployeeId ?? 0, LocationId = _query.LocationId, BookingId = null, StartTime = _query.EndTime, EndTime = _query.EndTime.AddHours(1) };
        List<CalendarEvent> events = [startOfEvent];

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object, _activityRecorder.Object,
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

        var sut = new AddVacancyCommand(_repoMock.Object, _transactionMock.Object, _activityRecorder.Object,
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

}
