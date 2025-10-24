namespace Klinkby.Booqr.Application.Models;

public sealed record JwtSettings
{
    [Required] public string? Key { get; set; }

    [Required] public string? Issuer { get; set; }

    [Required] public string? Audience { get; set; }
}
