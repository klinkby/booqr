namespace Klinkby.Booqr.Application;

public abstract partial class UpdateCommand<TRequest, TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<TRequest>
    where TRequest : AuthenticatedRequest, IId
{
    public Task Execute(TRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        LogUserUpdateTypeName(logger, query.AuthenticatedUserId, nameof(TItem), query.Id);
        TItem item = Map(query);
        return repository.Update(item, cancellation);
    }

    protected abstract TItem Map(TRequest query);

    [LoggerMessage(LogLevel.Information, "User {UserId} update {Type}:{Id}")]
    private static partial void LogUserUpdateTypeName(ILogger logger, int userId, string type, int id);
}
