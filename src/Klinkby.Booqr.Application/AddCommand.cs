namespace Klinkby.Booqr.Application;

public abstract partial class AddCommand<TRequest, TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<TRequest, Task<int>>
    where TRequest : AuthenticatedRequest
{
    public async Task<int> Execute(TRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        TItem item = Map(query);
        var newId = await repository.Add(item, cancellation);
        LogUserCreateTypeId(logger, query.UserName, nameof(TItem), newId);

        return newId;
    }

    protected abstract TItem Map(TRequest query);

    [LoggerMessage(LogLevel.Information, "User {User} created {Type}:{Id}")]
    private static partial void LogUserCreateTypeId(ILogger logger, string? User, string Type, int Id);
}
