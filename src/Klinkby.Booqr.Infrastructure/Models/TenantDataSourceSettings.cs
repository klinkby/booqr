using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Infrastructure.Models;

/// <summary>
///     Configuration for <see cref="Services.TenantDataSourceFactory" />. Bound from the
///     <c>TenantDataSources</c> configuration section by
///     <see cref="Microsoft.Extensions.DependencyInjection.TenantDataSourceServiceCollectionExtensions.AddTenantDataSources" />.
/// </summary>
/// <remarks>
///     This is deliberately a standalone settings type (not part of <c>InfrastructureSettings</c> or
///     <c>TenancySettings</c>) to avoid a merge conflict with subtask 2c, which owns
///     <c>ServiceCollectionExtensions.cs</c> / <c>TenancySettings</c>. 2c may fold these properties
///     into <c>TenancySettings</c> instead, as long as an
///     <see cref="IOptions{TOptions}" />&lt;<see cref="TenantDataSourceSettings" />&gt; remains
///     resolvable.
/// </remarks>
internal sealed record TenantDataSourceSettings
{
    /// <summary>
    ///     Gets or initializes the base PostgreSQL connection string (host/socket + database only —
    ///     no <c>Username</c>/<c>Password</c>, which the factory sets per tenant). Reuse the same
    ///     unix-socket host as the existing registry/superuser connection string.
    /// </summary>
    [Required]
    public required string BaseConnectionString { get; set; }

    /// <summary>
    ///     Gets or initializes the shared HMAC key used to derive each tenant role's password. Never
    ///     logged.
    /// </summary>
    [Required]
    public required string MasterSecret { get; set; }

    /// <summary>
    ///     Gets or initializes the per-tenant Npgsql <c>Max Pool Size</c>. Design calls for 2–3.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxPoolSize { get; set; } = 3;

    /// <summary>
    ///     Gets or initializes the bound size of the tenant <see cref="Npgsql.NpgsqlDataSource" /> LRU
    ///     cache (distinct tenant pools kept warm at once).
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxCacheEntries { get; set; } = 64;
}

[OptionsValidator]
internal sealed partial class ValidateTenantDataSourceSettings : IValidateOptions<TenantDataSourceSettings>;
