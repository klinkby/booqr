namespace Klinkby.Booqr.Application;

public abstract class GetCollectionCommand<TRequest, TItem>(IRepository<TItem, int> repository)
    : ICommand<TRequest, IAsyncEnumerable<TItem>>
    where TRequest : IPageQuery
{
    public IAsyncEnumerable<TItem> Execute(TRequest query, CancellationToken cancellation = default) =>
        repository.GetAll(query, cancellation);
}
