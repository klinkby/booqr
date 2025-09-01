namespace Klinkby.Booqr.Application.Services;

public sealed partial class DeleteServiceCommand(
    IServiceRepository services,
    ILogger<DeleteServiceCommand> logger)
    : ICommand<AuthenticatedByIdRequest>
{
    private readonly ILogger<DeleteServiceCommand> _logger = logger;

    public async Task Execute(AuthenticatedByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        LogUserDeleteTypeName(query.UserName, nameof(Service), query.Id);

        await services.Delete(query.Id, cancellation);
    }

    [LoggerMessage(LogLevel.Information, "User {User} delete {Type} {Id}")]
    private partial void LogUserDeleteTypeName(string? User, string Type, int Id);
}
