namespace Klinkby.Booqr.Application;

public abstract partial class DeleteCommand<TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<AuthenticatedByIdRequest>
{
    public Task Execute(AuthenticatedByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Delete(query, cancellation);
    }

    internal virtual Task<bool> Delete(AuthenticatedByIdRequest query, CancellationToken cancellation)
    {
        LogUserDeleteTypeName(logger, query.AuthenticatedUserId, nameof(Location), query.Id);
        return repository.Delete(query.Id, cancellation);
    }

    [LoggerMessage(180, LogLevel.Information, "User {UserId} delete {Type}:{Id}")]
    private static partial void LogUserDeleteTypeName(ILogger logger, int userId, string type, int id);
}
