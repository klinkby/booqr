namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
/// Abstract base class for commands that add new entities to the repository.
/// </summary>
/// <typeparam name="TRequest">The type of the authenticated request containing the data to add.</typeparam>
/// <typeparam name="TItem">The type of the entity to add to the repository.</typeparam>
/// <param name="repository">The repository for persisting entities.</param>
/// <param name="activityRecorder">The activity recorder for tracking user actions.</param>
/// <param name="logger">The logger for recording operations.</param>
public abstract partial class AddCommand<TRequest, TItem>(
    IRepository<TItem, int> repository,
    IActivityRecorder activityRecorder,
    ILogger logger)
    : ICommand<TRequest, Task<int>>
    where TRequest : AuthenticatedRequest
    where TItem : notnull
{
    private readonly LoggerMessages _log = new(logger);

    /// <summary>
    /// Executes the add command, creating a new entity in the repository.
    /// </summary>
    /// <param name="query">The authenticated request containing the data to add.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the ID of the newly created entity.</returns>
    public async Task<int> Execute(TRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        TItem item = Map(query);
        var newId = await repository.Add(item, cancellation);
        _log.UserCreateItem(query.AuthenticatedUserId, item.GetType().Name, newId);
        activityRecorder.Add<TItem>(new(query.AuthenticatedUserId, newId));
        return newId;
    }

    /// <summary>
    /// Maps the request to an entity that can be persisted.
    /// </summary>
    /// <param name="query">The request containing the data to map.</param>
    /// <returns>The mapped entity ready for persistence.</returns>
    protected abstract TItem Map(TRequest query);

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(170, LogLevel.Information, "User {UserId} created {Type}:{Id}")]
        public partial void UserCreateItem(int userId, string type, int id);
    }
}
