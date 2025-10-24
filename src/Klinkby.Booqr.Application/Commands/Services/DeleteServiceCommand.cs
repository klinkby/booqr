namespace Klinkby.Booqr.Application.Commands.Services;

public sealed class DeleteServiceCommand(
    IServiceRepository services,
    ILogger<DeleteServiceCommand> logger)
    : Abstractions.DeleteCommand<Service>(services, logger);
