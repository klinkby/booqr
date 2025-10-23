namespace Klinkby.Booqr.Application.Abstractions;

public abstract partial class UpdateCommand<TRequest, TItem>(IRepository<TItem, int> repository, ILogger logger)
    : ICommand<TRequest>
    where TRequest : AuthenticatedRequest, IId
    where TItem : notnull
{
    private readonly LoggerMessages _log = new(logger);

    public async Task Execute(TRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        TItem item = Map(query);
        _log.UpdateItem(query.AuthenticatedUserId, item.GetType().Name, query.Id);
        var updated = await repository.Update(item, cancellation);
        if (!updated)
        {
            throw new MidAirCollisionException($"{item.GetType().Name} {query.Id} was already updated.");
        }
    }

    protected abstract TItem Map(TRequest query);


    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(190, LogLevel.Information, "User {UserId} update {Type}:{Id}")]
        public partial void UpdateItem(int userId, string type, int id);
    }
}
