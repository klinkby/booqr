namespace Klinkby.Booqr.Application.Abstractions;

public abstract class GetByIdCommand<TItem>(IRepository<TItem, int> repository)
    : ICommand<ByIdRequest, Task<TItem?>>
{
    public Task<TItem?> Execute(ByIdRequest query, CancellationToken cancellation = default)
    {
        return repository.GetById(query.Id, cancellation);
    }
}
