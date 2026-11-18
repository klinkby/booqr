namespace Klinkby.Booqr.Application.Commands.Activities;

public sealed record GetActivitiesRequest(
    DateTime? FromTime,
    DateTime? ToTime,
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100) : IPageQuery;

public sealed class GetActivityCollectionCommand(IActivityRepository activities, TimeProvider timeProvider)
    : ICommand<GetActivitiesRequest, Task<List<Activity>>>
{
    public Task<List<Activity>> Execute(GetActivitiesRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return activities.GetRange(
                query.FromTime ?? timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
                query.ToTime ?? DateTime.MaxValue,
                query,
                cancellation)
            .ToListAsync(cancellation)
            .AsTask();
    }
}
