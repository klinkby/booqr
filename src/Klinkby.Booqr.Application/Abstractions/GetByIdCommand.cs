namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
/// Abstract base class for commands that retrieve a single entity by its ID from the repository.
/// </summary>
/// <typeparam name="TItem">The type of the entity to retrieve from the repository.</typeparam>
/// <param name="repository">The repository for retrieving entities.</param>
public abstract class GetByIdCommand<TItem>(IRepository<TItem, int> repository)
    : ICommand<ByIdRequest, Task<TItem?>>
{
    /// <summary>
    /// Executes the get-by-id command, retrieving a single entity from the repository.
    /// </summary>
    /// <param name="query">The request containing the ID of the entity to retrieve.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the entity if found; otherwise, <c>null</c>.</returns>
    public Task<TItem?> Execute(ByIdRequest query, CancellationToken cancellation = default)
    {
        return repository.GetById(query.Id, cancellation);
    }
}
