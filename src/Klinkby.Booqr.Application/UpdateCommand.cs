namespace Klinkby.Booqr.Application;

public abstract partial class UpdateCommand<TRequest, TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<TRequest>
    where TRequest : AuthenticatedRequest, IId
    where TItem : notnull
{
    public async Task Execute(TRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        TItem item = Map(query);
        LogUserUpdateTypeName(logger, query.AuthenticatedUserId, item.GetType().Name, query.Id);
        var updated = await repository.Update(item, cancellation);
        if (!updated) throw new MidAirCollisionException($"{item.GetType().Name} {query.Id} was already updated.");
    }

    protected abstract TItem Map(TRequest query);

    [LoggerMessage(LogLevel.Information, "User {UserId} update {Type}:{Id}")]
    private static partial void LogUserUpdateTypeName(ILogger logger, int userId, string type, int id);
}
