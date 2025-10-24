namespace Klinkby.Booqr.Application.Commands.Services;

public sealed class GetServiceCollectionCommand(
    IServiceRepository services)
    : GetCollectionCommand<PageQuery, Service>(services);
