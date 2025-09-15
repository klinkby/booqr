namespace Klinkby.Booqr.Application;

public abstract partial class DeleteCommand<TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<AuthenticatedByIdRequest>
{
    private readonly LoggerMessages _log = new(logger);

    public Task Execute(AuthenticatedByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Delete(query, cancellation);
    }

    internal virtual Task<bool> Delete(AuthenticatedByIdRequest query, CancellationToken cancellation)
    {
        _log.DeleteItem(query.AuthenticatedUserId, nameof(Location), query.Id);
        return repository.Delete(query.Id, cancellation);
    }


    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(180, LogLevel.Information, "User {UserId} delete {Type}:{Id}")]
        public partial void DeleteItem(int userId, string type, int id);
    }
}
