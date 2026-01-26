namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public sealed class ActivityRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IActivityRepository _sut = serviceProvider.Services.GetRequiredService<IActivityRepository>();
    private readonly IUserRepository _users = serviceProvider.Services.GetRequiredService<IUserRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_Activity_WHEN_Add_THEN_CanBeReadBack(Activity expected, User user)
    {
        long newId;
        Activity? actual;
        await _transaction.Begin();
        try
        {
            int userId = await _users.Add(user);
            expected = expected with { UserId = userId };
            newId = await _sut.Add(expected, CancellationToken.None);
            actual = await _sut.GetById(newId, CancellationToken.None);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.InRange(newId, 1, long.MaxValue);
        Assert.Equal(expected with { Id = actual!.Id }, actual);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_MultipleActivities_WHEN_GetAll_THEN_ReturnsAllActivities(
        Activity activity1, Activity activity2, Activity activity3, User user)
    {
        await _transaction.Begin();
        List<Activity> actuals;
        try
        {
            int userId = await _users.Add(user);
            activity1 = activity1 with { UserId = userId };
            activity2 = activity2 with { UserId = userId };
            activity3 = activity3 with { UserId = userId };

            await _sut.Add(activity1, CancellationToken.None);
            await _sut.Add(activity2, CancellationToken.None);
            await _sut.Add(activity3, CancellationToken.None);

            actuals = await _sut.GetAll(new PageQuery(), CancellationToken.None).ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.True(actuals.Count >= 3);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_ActivitiesInTimeRange_WHEN_GetRange_THEN_ReturnsOnlyActivitiesInRange(
        DateTime t0, Activity activity1, Activity activity2, Activity activity3, User user)
    {
        await _transaction.Begin();
        List<Activity> actuals;
        DateTime startTime = t0.AddHours(-2);
        DateTime midTime = t0;
        DateTime endTime = t0.AddHours(2);
        try
        {
            int userId = await _users.Add(user);

            activity1 = activity1 with { UserId = userId, Timestamp = startTime };
            activity2 = activity2 with { UserId = userId, Timestamp = midTime };
            activity3 = activity3 with { UserId = userId, Timestamp = endTime };

            await _sut.Add(activity1, CancellationToken.None);
            await _sut.Add(activity2, CancellationToken.None);
            await _sut.Add(activity3, CancellationToken.None);

            actuals = await _sut.GetRange(
                startTime.AddMinutes(-30),
                midTime.AddMinutes(30),
                new PageQuery(),
                CancellationToken.None).ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Contains(actuals, a => a.Timestamp == startTime);
        Assert.Contains(actuals, a => a.Timestamp == midTime);
        Assert.DoesNotContain(actuals, a => a.Timestamp == endTime);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_ManyActivities_WHEN_GetAllWithPagination_THEN_ReturnsRequestedPage(
        Activity activity, User user)
    {
        await _transaction.Begin();
        List<Activity> firstPage;
        List<Activity> secondPage;
        try
        {
            int userId = await _users.Add(user);

            for (int i = 0; i < 5; i++)
            {
                await _sut.Add(activity with { UserId = userId, EntityId = i }, CancellationToken.None);
            }

            firstPage = await _sut.GetAll(new PageQuery(0, 2), CancellationToken.None).ToListAsync();
            secondPage = await _sut.GetAll(new PageQuery(2, 2), CancellationToken.None).ToListAsync();
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Equal(2, firstPage.Count);
        Assert.Equal(2, secondPage.Count);
        Assert.NotEqual(firstPage[0].Id, secondPage[0].Id);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_ActivityWithNullRequestId_WHEN_Add_THEN_CanBeReadBack(Activity expected, User user)
    {
        long newId;
        Activity? actual;
        await _transaction.Begin();
        try
        {
            int userId = await _users.Add(user);
            expected = expected with { UserId = userId, RequestId = null };
            newId = await _sut.Add(expected, CancellationToken.None);
            actual = await _sut.GetById(newId, CancellationToken.None);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.InRange(newId, 1, long.MaxValue);
        Assert.Null(actual!.RequestId);
        Assert.Equal(expected with { Id = actual.Id }, actual);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_NonExistentId_WHEN_GetById_THEN_ReturnsNull(User user)
    {
        Activity? actual;
        await _transaction.Begin();
        try
        {
            await _users.Add(user);
            actual = await _sut.GetById(999999999L, CancellationToken.None);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Null(actual);
    }
}
