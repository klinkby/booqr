using Klinkby.Booqr.Core;
using Klinkby.Booqr.Infrastructure.Models;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Registers the cross-tenant <c>booqr_batch</c> (<c>BYPASSRLS</c>) data source and the scoped
///     <see cref="IBatchScope" /> opt-in, and re-registers the scoped keyed <see cref="DbConnection" />
///     (keyed <c>nameof(ConnectionProvider)</c>) so a scope with <see cref="IBatchScope.IsEnabled" />
///     set connects as <c>booqr_batch</c> instead of requiring a resolved tenant.
/// </summary>
/// <remarks>
///     <para>
///         <b>Call order:</b> call <see cref="AddBatchDataSource" /> <i>after</i>
///         <c>AddTenantDataSources</c> (subtask 2a) - it re-registers the same keyed
///         <see cref="DbConnection" /> service, and Microsoft.DI resolves a non-collection dependency
///         using the last registration.
///     </para>
///     <para>
///         <b>Config keys</b> (bound from <see cref="BatchSettings" />, under the <c>"Batch"</c>
///         configuration section):
///     </para>
///     <list type="bullet">
///         <item><description><c>Batch:ConnectionString</c> - base connection string (host/socket/database), no credentials.</description></item>
///         <item><description><c>Batch:BatchUsername</c> - batch login role; defaults to <c>booqr_batch</c>.</description></item>
///         <item><description><c>Batch:BatchPassword</c> - the role's password. Never logged.</description></item>
///     </list>
///     <para>
///         <b>Opt-in usage:</b> a background service creates its scope, resolves
///         <see cref="IBatchScope" /> and calls <see cref="IBatchScope.Enable" /> <i>before</i>
///         resolving any repository from that scope. Never enable this for a request-handling scope.
///     </para>
/// </remarks>
public static partial class BatchServiceCollectionExtensions
{
    /// <summary>
    ///     The keyed-service key under which the <c>booqr_batch</c> <see cref="NpgsqlDataSource" /> is
    ///     registered.
    /// </summary>
    public const string BatchDataSourceKey = "BatchDataSource";

    public static IServiceCollection AddBatchDataSource(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddSingleton<IValidateOptions<BatchSettings>, ValidateBatchSettings>()
            .AddOptions<BatchSettings>()
            .Bind(configuration.GetSection("Batch"))
            .ValidateOnStart();

        services.AddNpgsqlSlimDataSource(
            "",
            (serviceProvider, builder) =>
            {
                BatchSettings settings = serviceProvider.GetRequiredService<IOptions<BatchSettings>>().Value;
                builder.ConnectionStringBuilder.ConnectionString = settings.ConnectionString;
                builder.ConnectionStringBuilder.Username = settings.BatchUsername;
                builder.ConnectionStringBuilder.Password = settings.BatchPassword;
                Batch(
                    serviceProvider.GetRequiredService<ILogger<BatchSettings>>(),
                    builder.ConnectionStringBuilder.Host);
            }, serviceKey: BatchDataSourceKey);

        // Scoped opt-in: defaults to disabled, so request-handling scopes are unaffected unless a
        // background service explicitly enables it on its own scope.
        services.AddScoped<IBatchScope, BatchScope>();

        // Re-registers the scoped keyed DbConnection (see AddTenantDataSources): when the scope's
        // IBatchScope is enabled, connect as booqr_batch (BYPASSRLS, cross-tenant); otherwise fall
        // back to the tenant-aware connection, unchanged from subtask 2a.
        services.AddKeyedScoped<DbConnection>(nameof(ConnectionProvider), static (serviceProvider, _) =>
        {
            IBatchScope batchScope = serviceProvider.GetRequiredService<IBatchScope>();
            if (batchScope.IsEnabled)
            {
                NpgsqlDataSource batchDataSource =
                    serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(BatchDataSourceKey);
                return batchDataSource.CreateConnection();
            }

            TenantDataSourceLease lease = serviceProvider.GetRequiredService<TenantDataSourceLease>();
            return lease.DataSource.CreateConnection();
        });

        return services;
    }

    [LoggerMessage(1060, LogLevel.Information, "Batch (BYPASSRLS) connection is at {Host}")]
    private static partial void Batch(ILogger logger, string? host);
}
