namespace Klinkby.Booqr.Application.Models;

/// <summary>
/// Represents the configuration settings for JSON Web Token (JWT) authentication.
/// </summary>
public sealed record JwtSettings
{
    /// <summary>
    /// Gets or sets the secret key used for signing JWTs.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the issuer claim (iss) for JWTs.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the audience claim (aud) for JWTs.
    /// </summary>
    public required string Audience { get; set; }

    /// <summary>
    /// Gets or sets the expiration time span for JWTs. Defaults to 8 hours.
    /// </summary>
    public TimeSpan Expires { get; set; } = TimeSpan.FromHours(8);
}
