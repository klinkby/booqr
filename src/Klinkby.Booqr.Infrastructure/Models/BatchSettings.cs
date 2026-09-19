using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Infrastructure.Models;

/// <summary>
///     Configuration for the cross-tenant <c>booqr_batch</c> (<c>BYPASSRLS</c>) data source used by
///     background/scheduled work (reminder mail, refresh-token flushing, the
///     <c>ActivityBackgroundService</c> consumer). See <c>docs/1-design.md</c> "Background /
///     scheduled work" and <c>docs/2-implementation.md</c> Phase 3c.
/// </summary>
/// <remarks>
///     This is a separate connection from both the per-tenant data sources (subtask 2a) and the
///     registry data source (subtask 2b); it deliberately bypasses row-level security and must never
///     be used to serve a per-request/per-tenant scope.
/// </remarks>
internal sealed record BatchSettings
{
    /// <summary>
    ///     Gets or initializes the base PostgreSQL connection string (host/socket/database), without
    ///     credentials - the <c>booqr_batch</c> username and password are applied from
    ///     <see cref="BatchUsername" />/<see cref="BatchPassword" />. Reuses the same unix-socket host
    ///     as the registry/tenant connections.
    /// </summary>
    [Required]
    public required string ConnectionString { get; set; }

    /// <summary>
    ///     Gets or initializes the batch role's login name.
    /// </summary>
    /// <value>Defaults to <c>booqr_batch</c>, matching the role created by initdb.</value>
    [Required]
    public string BatchUsername { get; set; } = "booqr_batch";

    /// <summary>
    ///     Gets or initializes the batch role's password.
    /// </summary>
    /// <value>The password for the <c>booqr_batch</c> login role. Never logged.</value>
    [Required]
    public required string BatchPassword { get; set; }
}

[OptionsValidator]
internal sealed partial class ValidateBatchSettings : IValidateOptions<BatchSettings>;
