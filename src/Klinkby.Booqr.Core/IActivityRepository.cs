namespace Klinkby.Booqr.Core;

/// <summary>
/// Represents an activity log entry that records user interactions within the system.
/// </summary>
/// <remarks>
/// The <see cref="Activity"/> record provides information about a specific user action,
/// including the entity, action type, and when it occurred. It implements <see cref="IId"/>
/// for consistent identification.
/// </remarks>
/// <param name="Id">A unique identifier for the activity entry.</param>
/// <param name="Timestamp">The date and time of the activity.</param>
/// <param name="RequestId">An optional identifier for the HTTP-request associated with the activity entry.</param>
/// <param name="UserId">The identifier of the user who performed the activity.</param>
/// <param name="Entity">The name of the entity associated with the activity.</param>
/// <param name="EntityId">The identifier of the specific entity instance associated with the activity.</param>
/// <param name="Action">The type of action performed on the entity.</param>
public sealed record Activity(
    long Id,
    DateTime Timestamp,
    string? RequestId,
    int UserId,
    string Entity,
    int EntityId,
    string Action
);

public interface IActivityRepository : IImmutableRepository<Activity, long>
{
    IAsyncEnumerable<Activity> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        CancellationToken cancellation = default);
}
