namespace Klinkby.Booqr.Application.Services;

public sealed class GetServiceByIdCommand(
    IServiceRepository services)
    : ICommand<ByIdRequest, Task<Service?>>
{
    public Task<Service?> Execute(ByIdRequest query, CancellationToken cancellation = default)
    {
        return services.GetById(query.Id, cancellation);
    }
}
