namespace Klinkby.Booqr.Application.Commands.Services;

public sealed class GetServiceByIdCommand(
    IServiceRepository services)
    : GetByIdCommand<Service>(services);
