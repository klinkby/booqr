using Klinkby.Booqr.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Tests;

[Collection(nameof(ServiceProviderFixture))]
public sealed class CalendarRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly ICalendarRepository _sut = serviceProvider.Services.GetRequiredService<ICalendarRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_EventsOverlaps_WhenAddingSameEmployee_THEN_ConstraintFails(DateTime startDate,
        TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        await _transaction.Begin(CancellationToken.None);
        try
        {
            var employeeId = serviceProvider.TestData.EmployeeId1;
            await _sut.Add(new CalendarEvent(employeeId, serviceProvider.TestData.LocationId, null, startDate,
                endDate));
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _sut.Add(new CalendarEvent(employeeId, serviceProvider.TestData.LocationId, null, startDate, endDate))
            );
            Assert.Equal("no_overlapping_events", ex.ConstraintName);
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_EventsOverlaps_WhenUndeletingSameEmployee_THEN_ConstraintFails(DateTime startDate,
        TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        await _transaction.Begin(CancellationToken.None);
        try
        {
            var employeeId = serviceProvider.TestData.EmployeeId1;
            var eventId = await _sut.Add(new CalendarEvent(employeeId, serviceProvider.TestData.LocationId, null,
                startDate, endDate));
            await _sut.Delete(eventId);
            await _sut.Add(new CalendarEvent(employeeId, serviceProvider.TestData.LocationId, null, startDate,
                endDate));
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _sut.Undelete(eventId)
            );
            Assert.Equal("no_overlapping_events", ex.ConstraintName);
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_AdjacentEvent_WhenAddingSameEmployee_THEN_Succeeds(DateTime startDate, TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        await _transaction.Begin(CancellationToken.None);
        try
        {
            var employeeId = serviceProvider.TestData.EmployeeId1;
            await _sut.Add(new CalendarEvent(employeeId, serviceProvider.TestData.LocationId, null, default,
                startDate));
            await _sut.Add(new CalendarEvent(employeeId, serviceProvider.TestData.LocationId, null, startDate,
                endDate));
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_EventsOverlaps_WhenAddingAdjacent_THEN_Succeeds(DateTime startDate, TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        await _transaction.Begin(CancellationToken.None);
        try
        {
            await _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId1, serviceProvider.TestData.LocationId,
                null, startDate, endDate));
            await _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId2, serviceProvider.TestData.LocationId,
                null, startDate, endDate));
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_EventsOverlaps_WhenAddingDeleted_THEN_Succeeds(DateTime startDate, TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        await _transaction.Begin();
        try
        {
            var employeeId = serviceProvider.TestData.EmployeeId1;
            var eventId = await _sut.Add(new CalendarEvent(employeeId, serviceProvider.TestData.LocationId, null,
                startDate, endDate));
            await _sut.Delete(eventId);
            await _sut.Add(new CalendarEvent(employeeId, serviceProvider.TestData.LocationId, null, startDate,
                endDate));
        }
        finally
        {
            await _transaction.Rollback();
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_EventsOverlaps_WhenModifyingToSameEmployee_THEN_ConstraintFails(DateTime startDate,
        TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        await _transaction.Begin(CancellationToken.None);
        try
        {
            await _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId1, serviceProvider.TestData.LocationId,
                null, startDate, endDate));
            CalendarEvent modifiedEvent = new(serviceProvider.TestData.EmployeeId2, serviceProvider.TestData.LocationId,
                null, startDate, endDate);
            var eventId = await _sut.Add(modifiedEvent);
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _sut.Update(modifiedEvent with { Id = eventId, EmployeeId = serviceProvider.TestData.EmployeeId1 })
            );
            Assert.Equal("no_overlapping_events", ex.ConstraintName);
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_EmptyEvent_WhenAdding_THEN_ConstraintFails(DateTime startDate)
    {
        await _transaction.Begin(CancellationToken.None);
        try
        {
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId1, serviceProvider.TestData.LocationId,
                    null, startDate, startDate))
            );
            Assert.Equal("valid_time_range", ex.ConstraintName);
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_GetById_THEN_Succeeds(DateTime startDate, TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        CalendarEvent? actual;
        CalendarEvent calendarEvent = new(serviceProvider.TestData.EmployeeId1, serviceProvider.TestData.LocationId,
            null, startDate, endDate);
        var id = 0;

        await _transaction.Begin(CancellationToken.None);
        try
        {
            id = await _sut.Add(calendarEvent);
            actual = await _sut.GetById(id);
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }

        Assert.Equal(actual!.Id, id);
        Assert.Equal(calendarEvent.EmployeeId, actual.EmployeeId);
        Assert.Equal(calendarEvent.StartTime, actual.StartTime, TimeSpan.FromMilliseconds(1));
        Assert.Equal(calendarEvent.EndTime, actual.EndTime, TimeSpan.FromMilliseconds(1));
        Assert.NotEqual(actual.Created, default);
        Assert.NotEqual(actual.Modified, default);
        Assert.Null(actual.Deleted);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_GetAll_THEN_Succeeds(DateTime startDate, TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        await _transaction.Begin(CancellationToken.None);
        try
        {
            var goneId = await _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId1,
                serviceProvider.TestData.LocationId, null, startDate, endDate));
            await _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId2, serviceProvider.TestData.LocationId,
                null, startDate, endDate));
            await _sut.Delete(goneId);
            await _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId1, serviceProvider.TestData.LocationId,
                null, startDate, endDate));

            CalendarEvent[] actual = await _sut.GetAll(new PageQuery()).ToArrayAsync();

            Assert.Equal(2, actual.Length);
            Assert.All(actual, actualItem => Assert.NotEqual(0, actualItem.Id));

            actual = await _sut.GetAll(new PageQuery(Num: 1)).ToArrayAsync();
            Assert.Single(actual);
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_GetRange_THEN_Succeeds(DateTime startDate, TimeSpan duration)
    {
        DateTime endDate = startDate.Add(duration);
        await _transaction.Begin(CancellationToken.None);
        try
        {
            await _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId1, serviceProvider.TestData.LocationId,
                null, default, startDate));
            await _sut.Add(new CalendarEvent(serviceProvider.TestData.EmployeeId1, serviceProvider.TestData.LocationId,
                null, startDate, endDate));

            CalendarEvent[] actual =
                await _sut.GetRange(default, startDate.AddSeconds(-1), new PageQuery()).ToArrayAsync();
            Assert.Single(actual);

            actual = await _sut.GetRange(startDate.AddSeconds(1), endDate.AddYears(1), new PageQuery()).ToArrayAsync();
            Assert.Single(actual);

            actual = await _sut.GetRange(endDate.AddSeconds(1), endDate.AddYears(1), new PageQuery()).ToArrayAsync();
            Assert.Empty(actual);
        }
        finally
        {
            await _transaction.Rollback(CancellationToken.None);
        }
    }
}
