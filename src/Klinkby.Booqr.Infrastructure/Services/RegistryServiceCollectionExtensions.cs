using Klinkby.Booqr.Core;
using Klinkby.Booqr.Infrastructure.Models;
using Klinkby.Booqr.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Registers the tenant registry data source, <see cref="TenantRepository" />, and its
///     bounded-TTL resolution cache.
/// </summary>
/// <remarks>
///     Owned by subtask 2b (docs/2-implementation.md, Phase 2). This is intentionally a
///     <b>separate</b> extension from <c>AddInfrastructure</c> - it does not touch
///     <c>ServiceCollectionExtensions.AddInfrastructure</c> (owned by subtask 2c) or the tenant
///     data-source factory (owned by subtask 2a).
///     <para>
///     <b>Call order:</b> call <see cref="AddTenantRegistry" /> <i>after</i> <c>AddInfrastructure</c>.
///     <c>AddInfrastructure</c>'s <c>AddRepositories</c> (ServiceScan) auto-discovers the plain
///     <see cref="TenantRepository" /> and registers it as <see cref="ITenantRepository" />; calling
///     <see cref="AddTenantRegistry" /> afterwards re-registers <see cref="ITenantRepository" /> to
///     resolve through <see cref="CachingTenantRepository" /> instead, so callers resolving
///     <see cref="ITenantRepository" /> get the cached path without any change to
///     <see cref="TenantRepository" /> itself or to <c>AddInfrastructure</c>.
///     </para>
///     <para>
///     <b>Config keys</b> (bound from <see cref="RegistrySettings" />, under the <c>"Registry"</c>
///     configuration section, so they don't collide with <c>InfrastructureSettings</c>' own
///     root-level <c>ConnectionString</c> key bound by <c>AddInfrastructure</c>):
///     <list type="bullet">
///         <item><description><c>Registry:ConnectionString</c> - base registry connection string (host/socket/database), no credentials.</description></item>
///         <item><description><c>Registry:RegistryUsername</c> - registry login role; defaults to <c>booqr_registry</c>.</description></item>
///         <item><description><c>Registry:RegistryPassword</c> - registry role's password. Never logged.</description></item>
///         <item><description><c>Registry:CacheTtl</c> - bounded TTL for cached resolutions (positive and negative); defaults to 30s.</description></item>
///         <item><description><c>Registry:CacheSize</c> - max cache entries before oldest-eviction; defaults to 256.</description></item>
///     </list>
///     </para>
/// </remarks>
public static partial class RegistryServiceCollectionExtensions
{
    /// <summary>
    ///     The keyed-service key under which the registry <see cref="NpgsqlDataSource" /> is
    ///     registered. The registry data source connects as <c>booqr_registry</c> (SELECT-only on
    ///     <c>public.tenants</c>) and must never be confused with a tenant (<c>t_&lt;id&gt;</c>) data
    ///     source.
    /// </summary>
    public const string RegistryDataSourceKey = "RegistryDataSource";

    /// <summary>
    ///     Adds the registry data source, <see cref="TenantRepository" /> wiring, and the
    ///     bounded-TTL <see cref="CachingTenantRepository" /> decorator that <see cref="ITenantRepository" />
    ///     resolves through by default.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
    public static IServiceCollection AddTenantRegistry(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddSingleton<IValidateOptions<RegistrySettings>, ValidateRegistrySettings>()
            .AddOptions<RegistrySettings>()
            .Bind(configuration.GetSection("Registry"))
            .ValidateOnStart();

        services.AddNpgsqlSlimDataSource(
            "",
            (serviceProvider, builder) =>
            {
                RegistrySettings settings =
                    serviceProvider.GetRequiredService<IOptions<RegistrySettings>>().Value;
                builder.ConnectionStringBuilder.ConnectionString = settings.ConnectionString;
                builder.ConnectionStringBuilder.Username = settings.RegistryUsername;
                builder.ConnectionStringBuilder.Password = settings.RegistryPassword;
                Registry(
                    serviceProvider.GetRequiredService<ILogger<RegistrySettings>>(),
                    builder.ConnectionStringBuilder.Host);
            }, serviceKey: RegistryDataSourceKey);

        // TenantRepository and CachingTenantRepository are both auto-discovered as
        // ITenantRepository implementations by AddInfrastructure's AddRepositories (ServiceScan)
        // scan of IRepository-assignable types. Register TenantRepository again concretely
        // (harmless - same lifetime/ctor) so CachingTenantRepository can depend on the plain type
        // without recursing through ITenantRepository, then re-register ITenantRepository to
        // resolve through the caching decorator. Microsoft.DI resolves a non-collection dependency
        // using the last registration, so calling this method after AddInfrastructure makes the
        // cached path the one GetRequiredService<ITenantRepository>() returns.
        services.AddScoped<TenantRepository>();
        services.AddScoped<ITenantRepository, CachingTenantRepository>();

        return services;
    }

    [LoggerMessage(1050, LogLevel.Information, "Tenant registry is at {Host}")]
    private static partial void Registry(ILogger logger, string? host);
}
