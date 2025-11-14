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

/// <summary>
///     Provides data access operations for <see cref="Activity"/> log entries.
/// </summary>
public interface IActivityRepository : IImmutableRepository<Activity, long>
{
    /// <summary>
    ///     Retrieves activity log entries within the specified time range.
    /// </summary>
    /// <param name="fromTime">The start of the time range.</param>
    /// <param name="toTime">The end of the time range.</param>
    /// <param name="pageQuery">The pagination parameters.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream of <see cref="Activity"/> instances.</returns>
    IAsyncEnumerable<Activity> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        CancellationToken cancellation = default);
}
