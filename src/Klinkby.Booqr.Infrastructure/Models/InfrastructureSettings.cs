using System.ComponentModel.DataAnnotations;

namespace Klinkby.Booqr.Infrastructure.Models;

/// <summary>
///     Provides configuration settings for the infrastructure layer.
/// </summary>
/// <remarks>
///     This record contains all necessary configuration for database connections and email services.
///     All properties should be provided through configuration sources (e.g., appsettings.json).
/// </remarks>
internal sealed record InfrastructureSettings
{
    /// <summary>
    ///     Gets or initializes the PostgreSQL database connection string.
    /// </summary>
    /// <value>The connection string used to connect to the database.</value>
    [Required]
    public required string ConnectionString { get; set; }

    /// <summary>
    ///     Gets or initializes the base address for the EmailLabs API.
    /// </summary>
    /// <value>
    ///     The base URI for the mail client API. Defaults to <c>https://api.emaillabs.net.pl/</c>.
    /// </value>
    [Required]
    public Uri MailClientBaseAddress { get; set; } = new("https://api.emaillabs.net.pl/", UriKind.Absolute);

    /// <summary>
    ///     Gets or initializes the API key for authenticating with the EmailLabs service.
    /// </summary>
    /// <value>The API key used for Basic authentication.</value>
    [Required]
    public required string MailClientApiKey { get; set; }

    /// <summary>
    ///     Gets or initializes the account identifier for the EmailLabs service.
    /// </summary>
    /// <value>The account name or identifier for the mail service.</value>
    [Required]
    public required string MailClientAccount { get; set; }

    /// <summary>
    ///     Gets or initializes the sender email address for outgoing emails.
    /// </summary>
    /// <value>The email address that appears in the "From" field of sent emails.</value>
    [Required]
    public required string MailClientFromAddress { get; set; }
}
