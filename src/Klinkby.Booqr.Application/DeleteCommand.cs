namespace Klinkby.Booqr.Application;

public abstract partial class DeleteCommand<TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<AuthenticatedByIdRequest>
{
    public Task Execute(AuthenticatedByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        LogUserDeleteTypeName(logger, query.UserName, nameof(Location), query.Id);

        return repository.Delete(query.Id, cancellation);
    }

    [LoggerMessage(LogLevel.Information, "User {User} delete {Type}:{Id}")]
    private static partial void LogUserDeleteTypeName(ILogger logger, string? User, string Type, int Id);
}
