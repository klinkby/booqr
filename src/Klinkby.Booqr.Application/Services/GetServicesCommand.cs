namespace Klinkby.Booqr.Application.Services;

public sealed class GetServicesCommand(
    IServiceRepository services)
    : ICommand<PageQuery, IAsyncEnumerable<Service>>
{
    public IAsyncEnumerable<Service> Execute(PageQuery query, CancellationToken cancellation = default) =>
        services.GetAll(query, cancellation);
}
