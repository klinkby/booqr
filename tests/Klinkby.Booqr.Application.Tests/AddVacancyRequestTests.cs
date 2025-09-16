using Klinkby.Booqr.Application.Vacancies;

namespace Klinkby.Booqr.Application.Tests;

public class AddVacancyRequestTests
{
    [Theory]
    [AutoData]
    public void GIVEN_Intersecting_WHEN_TryExtendEnd_THEN_True(AddVacancyRequest request, CalendarEvent intersecting,
        DateTime t0)
    {
        // request from t to t+1h, intersecting starts t-1h and extends to request start
        request = request with { StartTime = t0, EndTime = t0 + TimeSpan.FromHours(1) };
        intersecting = intersecting with { StartTime = t0.AddHours(-1), EndTime = t0 };
        var result = request.TryExtendEnd(intersecting, out CalendarEvent? actual);
        Assert.True(result);
        Assert.Equal(intersecting.StartTime, actual!.StartTime);
        Assert.Equal(request.EndTime, actual.EndTime);
        Assert.Equal(intersecting.Id, actual.Id);
    }

    [Theory]
    [AutoData]
    public void GIVEN_Intersecting_WHEN_TryExtendStart_THEN_True(AddVacancyRequest request, CalendarEvent intersecting,
        DateTime t0)
    {
        // request from t to t+1h, intersecting starts at request.End and extends after
        request = request with { StartTime = t0, EndTime = t0 + TimeSpan.FromHours(1) };
        intersecting = intersecting with
        {
            StartTime = request.EndTime, EndTime = request.EndTime.AddHours(1), LocationId = request.LocationId
        };

        var result = request.TryExtendStart(intersecting, out CalendarEvent? actual);

        Assert.True(result);
        Assert.Equal(request.StartTime, actual!.StartTime);
        Assert.Equal(intersecting.EndTime, actual.EndTime);
        Assert.Equal(intersecting.Id, actual.Id);
    }

    [Theory]
    [AutoData]
    public void GIVEN_OverlappedEvents_WHEN_TryGetCompletelyOverlapped_THEN_True(AddVacancyRequest request,
        CalendarEvent e1, CalendarEvent e2, CalendarEvent outside, DateTime t0)
    {
        // request 10-12, e1 and e2 are within that window, outside is not
        request = request with { StartTime = t0, EndTime = t0.AddHours(2) };
        e1 = e1 with { StartTime = t0.AddMinutes(15), EndTime = t0.AddMinutes(45) };
        e2 = e2 with { StartTime = t0.AddMinutes(60), EndTime = t0.AddMinutes(90) };
        outside = outside with { StartTime = t0.AddHours(-1), EndTime = t0.AddHours(-0.5) };

        var events = new List<CalendarEvent> { e1, e2, outside };

        var result = request.TryGetCompletelyOverlapped(events, out IReadOnlyList<CalendarEvent>? obsolete);

        Assert.True(result);
        Assert.NotNull(obsolete);
        Assert.Contains(e1, obsolete!);
        Assert.Contains(e2, obsolete!);
        Assert.DoesNotContain(outside, obsolete!);
    }

    [Theory]
    [AutoData]
    public void GIVEN_CompletelyCovered_WHEN_TryGetCompletelyCovered_THEN_True(AddVacancyRequest request,
        CalendarEvent covering, CalendarEvent other, DateTime t0)
    {
        // request 10-12, covering is 9-13 with no booking, other covers but has a booking
        request = request with { StartTime = t0, EndTime = t0.AddHours(2) };
        covering = covering with { StartTime = t0.AddHours(-1), EndTime = t0.AddHours(3), BookingId = null };
        other = other with { StartTime = t0.AddHours(-2), EndTime = t0.AddHours(4), BookingId = 42 };

        var events = new List<CalendarEvent> { covering, other };

        var result = request.TryGetCompletelyCovered(events, out CalendarEvent? actual);

        Assert.True(result);
        Assert.NotNull(actual);
        Assert.Equal(covering.Id, actual!.Id);
        Assert.Equal(covering.StartTime, actual.StartTime);
        Assert.Equal(covering.EndTime, actual.EndTime);
    }

    [Theory]
    [AutoData]
    public void GIVEN_DifferentLocation_WHEN_TryGetEventWithConflictingLocation_THEN_True(AddVacancyRequest request,
        CalendarEvent otherLocation, DateTime t0)
    {
        // request at location L, one event at different location
        request = request with { StartTime = t0, EndTime = t0.AddHours(1) };
        otherLocation = otherLocation with
        {
            StartTime = t0.AddMinutes(30), EndTime = request.EndTime, LocationId = request.LocationId + 1
        };

        var events = new List<CalendarEvent> { otherLocation };
        var result = request.TryGetEventWithConflictingLocation(events, out CalendarEvent? conflict);

        Assert.True(result);
        Assert.NotNull(conflict);
        Assert.Equal(otherLocation.Id, conflict!.Id);
    }

    [Theory]
    [AutoData]
    public void GIVEN_DifferentLocation_WHEN_TryGetEventWithConflictingLocation_THEN_False(AddVacancyRequest request,
        CalendarEvent otherLocation, DateTime t0)
    {
        // request at location L, one event at another time, other event at same L
        request = request with { StartTime = t0, EndTime = t0.AddHours(1) };
        otherLocation = otherLocation with
        {
            StartTime = request.StartTime.AddMinutes(1),
            EndTime = request.EndTime.AddHours(2),
            LocationId = request.LocationId
        };

        var events = new List<CalendarEvent> { otherLocation };
        var result = request.TryGetEventWithConflictingLocation(events, out CalendarEvent? conflict);

        Assert.False(result);
        Assert.Null(conflict);
    }

    [Theory]
    [AutoData]
    public void GIVEN_DifferentTime_WHEN_TryGetEventWithConflictingLocation_THEN_False(AddVacancyRequest request,
        CalendarEvent otherTime, DateTime t0)
    {
        // request at location L, one event at another time, other event at same L
        request = request with { StartTime = t0, EndTime = t0.AddHours(1) };
        otherTime = otherTime with
        {
            StartTime = request.EndTime, EndTime = request.EndTime.AddHours(1), LocationId = request.LocationId
        };

        var events = new List<CalendarEvent> { otherTime };
        var result = request.TryGetEventWithConflictingLocation(events, out CalendarEvent? conflict);

        Assert.False(result);
        Assert.Null(conflict);
    }

    [Theory]
    [AutoData]
    public void GIVEN_TwoAdjacentEvents_WHEN_FindIntersecting_THEN_BothReturned(AddVacancyRequest request,
        CalendarEvent endOf, CalendarEvent startOf, DateTime t0)
    {
        // Arrange: request in the middle [t, t+1h]
        request = request with { StartTime = t0, EndTime = t0.AddHours(1) };

        // endOf: overlaps request start -> ends exactly at request.Start
        // startOf: overlaps request end -> starts exactly at request.End
        endOf = endOf with
        {
            StartTime = t0.AddHours(-1), EndTime = request.StartTime, LocationId = request.LocationId
        };
        startOf = startOf with
        {
            StartTime = request.EndTime, EndTime = request.EndTime.AddHours(1), LocationId = request.LocationId
        };

        var events = new List<CalendarEvent> { endOf, startOf };

        // Act
        (CalendarEvent? EndOf, CalendarEvent? StartOf) = request.FindIntersecting(events);

        // Assert
        Assert.NotNull(EndOf);
        Assert.NotNull(StartOf);
        Assert.Equal(endOf.Id, EndOf!.Id);
        Assert.Equal(startOf.Id, StartOf!.Id);
    }
}
