namespace Klinkby.Booqr.Application.Services;

public sealed record UpdateServiceRequest(
    [property: Required]
    [property: Range(1, int.MaxValue)]
    int Id,
    string Name) : AddServiceRequest(Name);

public sealed partial class UpdateServiceCommand(
    IServiceRepository services,
    ILogger<UpdateServiceCommand> logger)
    : ICommand<UpdateServiceRequest>
{
    private readonly ILogger<UpdateServiceCommand> _logger = logger;

    public async Task Execute(UpdateServiceRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        LogUserUpdateTypeName(query.UserName, nameof(Service), query.Id, query.Name);

        var item = new Service(query.Name)
        {
            Id = query.Id
        };
        _ = await services.Update(item, cancellation);
    }

    [LoggerMessage(LogLevel.Information, "User {User} update {Type}:{Id} {Name}")]
    private partial void LogUserUpdateTypeName(string? User, string Type, int Id, string Name);
}
