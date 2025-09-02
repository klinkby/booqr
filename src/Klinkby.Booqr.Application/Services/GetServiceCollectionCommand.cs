namespace Klinkby.Booqr.Application.Services;

public sealed class GetServiceCollectionCommand(
    IServiceRepository services)
    : GetCollectionCommand<PageQuery, Service>(services);
