using System.Diagnostics.CodeAnalysis;
namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
/// Abstract base class for commands that delete entities from the repository.
/// </summary>
/// <typeparam name="TItem">The type of the entity to delete from the repository.</typeparam>
/// <param name="repository">The repository for persisting entities.</param>
/// <param name="activityRecorder">The activity recorder for tracking user actions.</param>
/// <param name="logger">The logger for recording operations.</param>
public abstract partial class DeleteCommand<TItem>(
    IRepository<TItem, int> repository,
    IActivityRecorder activityRecorder,
    ILogger logger)
    : ICommand<AuthenticatedByIdRequest>
{
    private readonly LoggerMessages _log = new(logger);

    /// <summary>
    /// Executes the delete command, removing an entity from the repository.
    /// </summary>
    /// <param name="query">The authenticated request containing the ID of the entity to delete.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Execute(AuthenticatedByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var deleted = await Delete(query, cancellation);
        if (deleted) activityRecorder.Delete<TItem>(new (query.AuthenticatedUserId, query.Id));
    }

    internal virtual Task<bool> Delete(AuthenticatedByIdRequest query, CancellationToken cancellation)
    {
        _log.DeleteItem(query.AuthenticatedUserId, typeof(TItem).Name, query.Id);
        return repository.Delete(query.Id, cancellation);
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(180, LogLevel.Information, "User {UserId} delete {Type}:{Id}")]
        public partial void DeleteItem(int userId, string type, int id);
    }
}
