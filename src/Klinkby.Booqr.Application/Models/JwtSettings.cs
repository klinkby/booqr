using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Models;

/// <summary>
/// Represents the configuration settings for JSON Web Token (JWT) authentication.
/// </summary>
public sealed record JwtSettings
{
    /// <summary>
    /// Gets or sets the secret key used for signing JWTs.
    /// </summary>
    [Required, MinLength(32)]
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the issuer claim (iss) for JWTs.
    /// </summary>
    [Required]
    public required string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the audience claim (aud) for JWTs.
    /// </summary>
    [Required]
    public required string Audience { get; set; }

    /// <summary>
    /// Gets or sets the expiration time span for access JWTs. Defaults to 1 hour.
    /// </summary>
    [Required]
    public TimeSpan AccessExpires { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the expiration time span for refresh tokens. Defaults to 1 day.
    /// </summary>
    [Required]
    public TimeSpan RefreshExpires { get; set; } = TimeSpan.FromDays(1);
}

[OptionsValidator]
internal sealed partial class ValidateJwtSettings : IValidateOptions<JwtSettings>;
