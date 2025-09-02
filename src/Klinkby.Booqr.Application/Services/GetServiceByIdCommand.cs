namespace Klinkby.Booqr.Application.Services;

public sealed class GetServiceByIdCommand(
    IServiceRepository services)
    : GetByIdCommand<Service>(services);
