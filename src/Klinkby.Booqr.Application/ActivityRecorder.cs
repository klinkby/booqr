using System.Runtime.CompilerServices;
using Klinkby.Booqr.Core;
using Activity = Klinkby.Booqr.Core.Activity;

namespace Klinkby.Booqr.Application;

/// <summary>
/// Represents a query for recording an activity associated with an entity and user.
/// </summary>
/// <typeparam name="TItem">The type of the entity associated with the activity.</typeparam>
/// <param name="UserId">The ID of the user performing the activity.</param>
/// <param name="EntityId">The ID of the entity being acted upon.</param>
/// <param name="TenantId">The ID of the tenant in which the activity occurred.</param>
public record struct ActivityQuery<TItem>(int UserId, int EntityId, int TenantId);

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
    /// <param name="cancellation">A token to cancel the operation.</param>
    Task Add<TItem>(ActivityQuery<TItem> query, CancellationToken cancellation = default);

    /// <summary>
    /// Records an activity for updating an existing entity.
    /// </summary>
    /// <typeparam name="TItem">The type of the entity being updated.</typeparam>
    /// <param name="query">The activity query containing user and entity information.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    Task Update<TItem>(ActivityQuery<TItem> query, CancellationToken cancellation = default);

    /// <summary>
    /// Records an activity for deleting an entity.
    /// </summary>
    /// <typeparam name="TItem">The type of the entity being deleted.</typeparam>
    /// <param name="query">The activity query containing user and entity information.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    Task Delete<TItem>(ActivityQuery<TItem> query, CancellationToken cancellation = default);
}

/// <remarks>
/// Writes the activity synchronously on the request's own (tenant) connection via
/// <see cref="IActivityRepository.Record"/>, which is best-effort: a failed write is logged and
/// swallowed there, so audit recording never faults the caller's use case. The DB stamps
/// <c>tenant_id</c> via the RLS <c>DEFAULT app.tenant_of(current_user)</c>, so this needs no
/// cross-tenant (BYPASSRLS) access.
/// </remarks>
internal sealed class ActivityRecorder(
    IActivityRepository activities,
    TimeProvider timeProvider,
    IRequestMetadata? etagProvider = null
) : IActivityRecorder
{
    public Task Add<TItem>(ActivityQuery<TItem> query, CancellationToken cancellation = default) =>
        activities.Record(CreateActivity(query), cancellation);

    public Task Update<TItem>(ActivityQuery<TItem> query, CancellationToken cancellation = default) =>
        activities.Record(CreateActivity(query), cancellation);

    public Task Delete<TItem>(ActivityQuery<TItem> query, CancellationToken cancellation = default) =>
        activities.Record(CreateActivity(query), cancellation);

    private Activity CreateActivity<TItem>(ActivityQuery<TItem> query, [CallerMemberName] string action = "") =>
        new(0,
            timeProvider.GetUtcNow().UtcDateTime,
            etagProvider?.TraceId,
            query.UserId,
            typeof(TItem).Name,
            query.EntityId,
            action,
            query.TenantId);
}
