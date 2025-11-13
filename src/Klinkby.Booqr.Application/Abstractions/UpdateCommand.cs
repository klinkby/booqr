using Klinkby.Booqr.Core.Exceptions;

namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
/// Abstract base class for commands that update existing entities in the repository.
/// </summary>
/// <typeparam name="TRequest">The type of the authenticated request containing the update data.</typeparam>
/// <typeparam name="TItem">The type of the entity to update in the repository.</typeparam>
/// <param name="repository">The repository for persisting entities.</param>
/// <param name="activityRecorder">The activity recorder for tracking user actions.</param>
/// <param name="logger">The logger for recording operations.</param>
public abstract partial class UpdateCommand<TRequest, TItem>(
    IRepository<TItem, int> repository,
    IActivityRecorder activityRecorder,
    ILogger logger)
    : ICommand<TRequest>
    where TRequest : AuthenticatedRequest, IId
    where TItem : notnull
{
    private readonly LoggerMessages _log = new(logger);

    /// <summary>
    /// Executes the update command, modifying an existing entity in the repository.
    /// </summary>
    /// <param name="query">The authenticated request containing the update data.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="MidAirCollisionException">Thrown when the entity was modified by another operation (optimistic concurrency failure).</exception>
    public async Task Execute(TRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        TItem item = Map(query);
        _log.UpdateItem(query.AuthenticatedUserId, item.GetType().Name, query.Id);
        var updated = await repository.Update(item, cancellation);
        if (!updated)
        {
            throw new MidAirCollisionException($"{item.GetType().Name} {query.Id} was already updated.");
        }

        activityRecorder.Update<TItem>(new(query.AuthenticatedUserId, query.Id));
    }

    /// <summary>
    /// Maps the request to an entity that can be persisted.
    /// </summary>
    /// <param name="query">The request containing the data to map.</param>
    /// <returns>The mapped entity ready for persistence.</returns>
    protected abstract TItem Map(TRequest query);


    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(190, LogLevel.Information, "User {UserId} update {Type}:{Id}")]
        public partial void UpdateItem(int userId, string type, int id);
    }
}
