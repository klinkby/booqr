namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
/// Abstract base class for commands that retrieve a collection of entities from the repository with pagination support.
/// </summary>
/// <typeparam name="TRequest">The type of the request containing pagination parameters.</typeparam>
/// <typeparam name="TItem">The type of the entities to retrieve from the repository.</typeparam>
/// <param name="repository">The repository for retrieving entities.</param>
public abstract class GetCollectionCommand<TRequest, TItem>(IRepository<TItem, int> repository)
    : ICommand<TRequest, IAsyncEnumerable<TItem>>
    where TRequest : IPageQuery
{
    /// <summary>
    /// Executes the get-collection command, retrieving a paginated collection of entities from the repository.
    /// </summary>
    /// <param name="query">The request containing pagination parameters (start index and page size).</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous enumerable of entities.</returns>
    public IAsyncEnumerable<TItem> Execute(TRequest query, CancellationToken cancellation = default)
    {
        return repository.GetAll(query, cancellation);
    }
}
