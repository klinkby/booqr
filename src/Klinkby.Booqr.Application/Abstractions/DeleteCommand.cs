namespace Klinkby.Booqr.Application.Abstractions;

public abstract partial class DeleteCommand<TItem>(
    IRepository<TItem, int> repository,
    IActivityRecorder activityRecorder,
    ILogger logger)
    : ICommand<AuthenticatedByIdRequest>
{
    private readonly LoggerMessages _log = new(logger);

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

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(180, LogLevel.Information, "User {UserId} delete {Type}:{Id}")]
        public partial void DeleteItem(int userId, string type, int id);
    }
}
