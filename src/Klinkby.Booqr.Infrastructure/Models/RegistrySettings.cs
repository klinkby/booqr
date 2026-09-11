using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Infrastructure.Models;

/// <summary>
///     Provides configuration settings for the tenant registry data source.
/// </summary>
/// <remarks>
///     The registry connection resolves host/slug → tenant via <c>public.tenants</c> using the
///     <c>booqr_registry</c> role (SELECT-only). This is a separate connection from the per-tenant
///     data sources (owned by the tenant data-source factory, subtask 2a) and is never used to read
///     or write tenant-scoped (<c>app</c>) tables.
/// </remarks>
internal sealed record RegistrySettings
{
    /// <summary>
    ///     Gets or initializes the PostgreSQL connection string for the registry data source.
    /// </summary>
    /// <value>
    ///     The base connection string (host/socket/database), without credentials — the
    ///     <c>booqr_registry</c> username and password are applied from
    ///     <see cref="RegistryUsername" />/<see cref="RegistryPassword" />.
    /// </value>
    [Required]
    public required string ConnectionString { get; set; }

    /// <summary>
    ///     Gets or initializes the registry role's login name.
    /// </summary>
    /// <value>Defaults to <c>booqr_registry</c>, matching the role created by initdb.</value>
    [Required]
    public string RegistryUsername { get; set; } = "booqr_registry";

    /// <summary>
    ///     Gets or initializes the registry role's password.
    /// </summary>
    /// <value>The password for the <c>booqr_registry</c> login role. Never logged.</value>
    [Required]
    public required string RegistryPassword { get; set; }

    /// <summary>
    ///     Gets or initializes the bounded time-to-live for cached tenant resolutions (positive and
    ///     negative).
    /// </summary>
    /// <remarks>
    ///     No <c>[Range(typeof(TimeSpan), …)]</c> attribute: that RangeAttribute overload is
    ///     reflection-based (TypeConverter) and is flagged IL2026 under Native AOT/trimming. The
    ///     value binds from a config string (e.g. <c>"00:00:30"</c>); a malformed value fails binding,
    ///     and the 30-second default applies when unset.
    /// </remarks>
    /// <value>Defaults to 30 seconds.</value>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Gets or initializes the maximum number of entries retained in the resolution cache before
    ///     the oldest entries are evicted.
    /// </summary>
    /// <value>Defaults to 256.</value>
    [Range(1, int.MaxValue)]
    public int CacheSize { get; set; } = 256;
}

[OptionsValidator]
internal sealed partial class ValidateRegistrySettings : IValidateOptions<RegistrySettings>;
