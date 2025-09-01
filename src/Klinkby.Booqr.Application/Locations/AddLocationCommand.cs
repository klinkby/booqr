namespace Klinkby.Booqr.Application.Locations;

public record AddLocationRequest(
    [property: Required]
    [property: StringLength(0xff)]
    string Name) : AuthenticatedRequest;

public sealed partial class AddLocationCommand(
    ILocationRepository locations,
    ILogger<AddLocationCommand> logger)
    : ICommand<AddLocationRequest, Task<int>>
{
    private readonly ILogger<AddLocationCommand> _logger = logger;

    public async Task<int> Execute(AddLocationRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        LogUserCreateTypeName(query.UserName, nameof(Location), query.Name);

        Location item = new(
            query.Name);
        var newId = await locations.Add(item, cancellation);

        return newId;
    }

    [LoggerMessage(LogLevel.Information, "User {User} create {Type} {Name}")]
    private partial void LogUserCreateTypeName(string? User, string Type, string Name);
}
