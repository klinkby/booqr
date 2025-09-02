namespace Klinkby.Booqr.Application;

public class GetByIdCommand<TItem>(IRepository<TItem, int> repository)
    : ICommand<ByIdRequest, Task<TItem?>>
{
    public Task<TItem?> Execute(ByIdRequest query, CancellationToken cancellation = default) =>
        repository.GetById(query.Id, cancellation);
}
