namespace Klinkby.Booqr.Application.Commands.Services;

public sealed class DeleteServiceCommand(
    IServiceRepository services,
    ILogger<DeleteServiceCommand> logger)
    : DeleteCommand<Service>(services, logger);
