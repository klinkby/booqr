namespace Klinkby.Booqr.Infrastructure.Models;

public sealed record InfrastructureSettings
{
    public required string ConnectionString { get; init; }
    public required string MailClientApiKey { get; init; }
    public required string MailClientAccount { get; init; }
    public required string MailClientFromAddress { get; init; }
}
