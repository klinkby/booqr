using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Models;

/// <summary>
/// Represents the configuration settings for password handling.
/// </summary>
public sealed record PasswordSettings
{
    [Required, MinLength(32)]
    public required string HmacKey { get; set; }

    /// <summary>
    /// Gets or sets the path used for password reset operations. Defaults to "change-password".
    /// </summary>
    [Required]
    public string ResetPath { get; set; } = "/change-password";

    /// <summary>
    /// Gets or sets the timeout duration for password reset tokens. Defaults to 2 hours.
    /// </summary>
    [Required, Range(1, Int32.MaxValue)]
    public int ResetTimeoutHours { get; set; } = 2;

    /// <summary>
    /// Gets or sets the timeout duration for sign-up tokens. Defaults to 24 hours.
    /// </summary>
    [Required, Range(1, Int32.MaxValue)]
    public int SignUpTimeoutHours { get; set; } = 24;

}


[OptionsValidator]
internal sealed partial class ValidatePasswordSettings : IValidateOptions<PasswordSettings>;
