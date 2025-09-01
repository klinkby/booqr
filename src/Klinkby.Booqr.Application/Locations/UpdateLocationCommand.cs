namespace Klinkby.Booqr.Application.Locations;

public sealed record UpdateLocationRequest(
    [property: Range(1, int.MaxValue)] int Id,
    string Name) : AddLocationRequest(Name);

public sealed partial class UpdateLocationCommand(
    ILocationRepository locations,
    ILogger<UpdateLocationCommand> logger)
    : ICommand<UpdateLocationRequest>
{
    private readonly ILogger<UpdateLocationCommand> _logger = logger;

    public async Task Execute(UpdateLocationRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        LogUserUpdateTypeName(query.UserName, nameof(Location), query.Id, query.Name);

        var item = new Location(query.Name)
        {
            Id = query.Id
        };
        _ = await locations.Update(item, cancellation);
    }

    [LoggerMessage(LogLevel.Information, "User {User} update {Type}:{Id} {Name}")]
    private partial void LogUserUpdateTypeName(string? User, string Type, int Id, string Name);
}
