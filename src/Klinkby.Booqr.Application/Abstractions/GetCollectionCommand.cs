namespace Klinkby.Booqr.Application.Abstractions;

public abstract class GetCollectionCommand<TRequest, TItem>(IRepository<TItem, int> repository)
    : ICommand<TRequest, IAsyncEnumerable<TItem>>
    where TRequest : IPageQuery
{
    public IAsyncEnumerable<TItem> Execute(TRequest query, CancellationToken cancellation = default)
    {
        return repository.GetAll(query, cancellation);
    }
}
