namespace Klinkby.Booqr.Infrastructure.Models;

public sealed record InfrastructureSettings
{
    public required string ConnectionString { get; set; }
    public Uri MailClientBaseAddress { get; set; } = new("https://api.emaillabs.net.pl/", UriKind.Absolute);
    public required string MailClientApiKey { get; set; }
    public required string MailClientAccount { get; set; }
    public required string MailClientFromAddress { get; set; }
}
