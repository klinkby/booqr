namespace Klinkby.Booqr.Infrastructure.Models;

public sealed record InfrastructureSettings
{
    public string? ConnectionString { get; set; }
    public string? MailClientApiKey { get; set; }
    public string? MailClientAccount { get; set; }
    public string? MailClientFromAddress { get; set; }
}
