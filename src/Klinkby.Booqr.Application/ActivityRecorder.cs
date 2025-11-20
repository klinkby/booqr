using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Activity = Klinkby.Booqr.Core.Activity;

namespace Klinkby.Booqr.Application;

/// <summary>
/// Represents a query for recording an activity associated with an entity and user.
/// </summary>
/// <typeparam name="TItem">The type of the entity associated with the activity.</typeparam>
/// <param name="UserId">The ID of the user performing the activity.</param>
/// <param name="EntityId">The ID of the entity being acted upon.</param>
public record struct ActivityQuery<TItem>(int UserId, int EntityId);

/// <summary>
/// Provides methods for recording user activities on entities.
/// </summary>
public interface IActivityRecorder
{
    /// <summary>
    /// Records an activity for adding a new entity.
    /// </summary>
    /// <typeparam name="TItem">The type of the entity being added.</typeparam>
    /// <param name="query">The activity query containing user and entity information.</param>
    void Add<TItem>(ActivityQuery<TItem> query);

    /// <summary>
    /// Records an activity for updating an existing entity.
    /// </summary>
    /// <typeparam name="TItem">The type of the entity being updated.</typeparam>
    /// <param name="query">The activity query containing user and entity information.</param>
    void Update<TItem>(ActivityQuery<TItem> query);

    /// <summary>
    /// Records an activity for deleting an entity.
    /// </summary>
    /// <typeparam name="TItem">The type of the entity being deleted.</typeparam>
    /// <param name="query">The activity query containing user and entity information.</param>
    void Delete<TItem>(ActivityQuery<TItem> query);
}

internal sealed class ActivityRecorder(
    ChannelWriter<Activity> writer,
    TimeProvider timeProvider,
    IRequestMetadata? etagProvider = null
) : IActivityRecorder
{
    public void Add<TItem>(ActivityQuery<TItem> query)
    {
        Activity activity = CreateActivity(query);
        _ = writer.TryWrite(activity);
    }

    public void Update<TItem>(ActivityQuery<TItem> query)
    {
        Activity activity = CreateActivity(query);
        _ = writer.TryWrite(activity);
    }

    public void Delete<TItem>(ActivityQuery<TItem> query)
    {
        Activity activity = CreateActivity(query);
        _ = writer.TryWrite(activity);
    }

    private Activity CreateActivity<TItem>(ActivityQuery<TItem> query, [CallerMemberName] string action = "") =>
        new(0,
            timeProvider.GetUtcNow().UtcDateTime,
            etagProvider?.TraceId,
            query.UserId,
            typeof(TItem).Name,
            query.EntityId,
            action);
}
