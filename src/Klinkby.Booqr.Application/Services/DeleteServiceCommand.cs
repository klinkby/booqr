namespace Klinkby.Booqr.Application.Services;

public sealed class DeleteServiceCommand(
    IServiceRepository services,
    ILogger<DeleteServiceCommand> logger)
    : DeleteCommand<Service>(services, logger);
