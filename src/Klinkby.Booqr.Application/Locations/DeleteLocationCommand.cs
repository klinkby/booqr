namespace Klinkby.Booqr.Application.Locations;

public sealed partial class DeleteLocationCommand(
    ILocationRepository locations,
    ILogger<DeleteLocationCommand> logger)
    : ICommand<AuthenticatedByIdRequest>
{
    private readonly ILogger<DeleteLocationCommand> _logger = logger;

    public async Task Execute(AuthenticatedByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        LogUserDeleteTypeName(query.UserName, nameof(Location), query.Id);

        await locations.Delete(query.Id, cancellation);
    }

    [LoggerMessage(LogLevel.Information, "User {User} delete {Type} {Id}")]
    private partial void LogUserDeleteTypeName(string? User, string Type, int Id);
}
