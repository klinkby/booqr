namespace Klinkby.Booqr.Application.Services;

public record AddServiceRequest(
    [property: Required]
    [property: StringLength(0xff)]
    string Name
) : AuthenticatedRequest;

public sealed partial class AddServiceCommand(
    IServiceRepository services,
    ILogger<AddServiceCommand> logger)
    : ICommand<AddServiceRequest, Task<int>>
{
    private readonly ILogger<AddServiceCommand> _logger = logger;

    public async Task<int> Execute(AddServiceRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        LogUserCreateTypeName(query.UserName, nameof(Service), query.Name);

        Service item = new(query.Name);
        var newId = await services.Add(item, cancellation);

        return newId;
    }

    [LoggerMessage(LogLevel.Information, "User {User} create {Type} {Name}")]
    private partial void LogUserCreateTypeName(string? User, string Type, string Name);
}
