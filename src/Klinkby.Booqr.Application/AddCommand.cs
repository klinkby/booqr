namespace Klinkby.Booqr.Application;

public abstract partial class AddCommand<TRequest, TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<TRequest, Task<int>>
    where TRequest : AuthenticatedRequest
    where TItem : notnull
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<int> Execute(TRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        TItem item = Map(query);
        var newId = await repository.Add(item, cancellation);
        _log.UserCreateItem(query.AuthenticatedUserId, item.GetType().Name, newId);

        return newId;
    }

    protected abstract TItem Map(TRequest query);

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(170, LogLevel.Information, "User {UserId} created {Type}:{Id}")]
        public partial void UserCreateItem(int userId, string type, int id);
    }
}
