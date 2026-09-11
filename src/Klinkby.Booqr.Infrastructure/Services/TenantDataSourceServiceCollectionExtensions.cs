using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Registers the tenant-aware data-source factory and the tenant-aware scoped keyed
///     <see cref="DbConnection" />, per <c>docs/1-design.md</c> "Tenant data-source factory" /
///     "Request flow".
/// </summary>
/// <remarks>
///     <para>
///         <b>Coordination note (Phase 2a/2c split):</b> this file intentionally does NOT touch
///         <c>AddInfrastructure</c> in <c>ServiceCollectionExtensions.cs</c> (owned by subtask 2c) to
///         avoid a merge conflict. 2c must call <see cref="AddTenantDataSources" /> from
///         <c>AddInfrastructure</c> (or equivalent composition root) once <c>TenancySettings</c> exists.
///     </para>
///     <para>
///         <b>Config keys bound by this extension</b> (section name <c>TenantDataSources</c> — 2c may
///         instead flatten these onto <c>TenancySettings</c> and remove the standalone binding here,
///         as long as an <see cref="IOptions{TOptions}" />&lt;<see cref="TenantDataSourceSettings" />&gt;
///         is resolvable when <see cref="TenantDataSourceFactory" /> is constructed):
///     </para>
///     <list type="bullet">
///         <item><description><c>TenantDataSources:BaseConnectionString</c> — host/socket/database (no user/password), reused from the same unix-socket host as the existing registry/superuser connection string.</description></item>
///         <item><description><c>TenantDataSources:MasterSecret</c> — the HMAC key. Never logged; source from a secret store in production.</description></item>
///         <item><description><c>TenantDataSources:MaxPoolSize</c> — per-tenant Npgsql <c>Max Pool Size</c> (design calls for 2–3).</description></item>
///         <item><description><c>TenantDataSources:MaxCacheEntries</c> — bound size of the tenant <see cref="NpgsqlDataSource" /> LRU cache.</description></item>
///     </list>
///     <para>
///         <b>What this registers vs. what it depends on for <see cref="ITenantContext" />:</b> this
///         extension does NOT register any <see cref="ITenantContext" /> implementation — it only
///         consumes one from the container (resolved per-scope). The real implementation, populated by
///         the tenant-resolution middleware, is Phase 4 (API). Phase 2c/4 must register a scoped
///         <see cref="ITenantContext" /> exactly once; this file will throw a clear DI resolution error
///         if none is registered when a scope tries to open a tenant connection.
///     </para>
/// </remarks>
public static class TenantDataSourceServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the singleton <see cref="TenantDataSourceFactory" /> and replaces the scoped
    ///     keyed <see cref="DbConnection" /> (keyed <c>nameof(ConnectionProvider)</c>) with a
    ///     tenant-aware factory that reads the scoped <see cref="ITenantContext" />.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    ///     Configuration bound to <see cref="TenantDataSourceSettings" /> (see type-level remarks for
    ///     exact keys).
    /// </param>
    public static IServiceCollection AddTenantDataSources(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddSingleton<IValidateOptions<TenantDataSourceSettings>, ValidateTenantDataSourceSettings>()
            .AddOptions<TenantDataSourceSettings>()
            .Bind(configuration.GetSection("TenantDataSources"))
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            TenantDataSourceSettings settings = serviceProvider
                .GetRequiredService<IOptions<TenantDataSourceSettings>>().Value;
            return new TenantDataSourceFactory(
                settings.BaseConnectionString,
                settings.MasterSecret,
                settings.MaxPoolSize,
                settings.MaxCacheEntries);
        });

        // Scoped: acquires the tenant's data-source lease once per scope and releases it when the
        // scope ends (via IDisposable), independent of the DbConnection's own lifetime below.
        services.AddScoped(serviceProvider =>
        {
            ITenantContext tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
            if (!tenantContext.HasTenant)
            {
                throw new InvalidOperationException(
                    "Cannot open a tenant-scoped database connection: no tenant was resolved for this " +
                    "request scope. Tenant-required data access requires ITenantContext.HasTenant to be " +
                    "true; anonymous/registry paths must use the registry data source, not this scoped " +
                    "connection.");
            }

            TenantDataSourceFactory factory = serviceProvider.GetRequiredService<TenantDataSourceFactory>();
            return factory.Acquire(tenantContext.TenantId, tenantContext.DbRole);
        });

        // Scoped keyed DbConnection: one per scope (unopened — ConnectionProvider opens it lazily),
        // built from the leased data source above. Keeping the lease as a separate scoped service
        // ties its release to scope disposal without needing a custom DbConnection wrapper type.
        services.AddKeyedScoped<DbConnection>(nameof(ConnectionProvider), static (serviceProvider, _) =>
        {
            TenantDataSourceLease lease = serviceProvider.GetRequiredService<TenantDataSourceLease>();
            return lease.DataSource.CreateConnection();
        });

        return services;
    }
}
