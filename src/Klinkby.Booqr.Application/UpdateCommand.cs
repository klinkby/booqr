namespace Klinkby.Booqr.Application;

public abstract partial class UpdateCommand<TRequest, TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<TRequest>
    where TRequest : AuthenticatedRequest, IId
{
    public Task Execute(TRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        LogUserUpdateTypeName(logger, query.UserName, nameof(TItem), query.Id);
        TItem item = Map(query);
        return repository.Update(item, cancellation);
    }

    protected abstract TItem Map(TRequest query);

    [LoggerMessage(LogLevel.Information, "User {User} update {Type}:{Id}")]
    private static partial void LogUserUpdateTypeName(ILogger logger, string? User, string Type, int Id);
}
