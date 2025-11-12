namespace Klinkby.Booqr.Application.Models;

public sealed record JwtSettings
{
    public required string Key { get; set; }

    public required string Issuer { get; set; }

    public required string Audience { get; set; }

    public TimeSpan Expires { get; set; } = TimeSpan.FromHours(8);
}
