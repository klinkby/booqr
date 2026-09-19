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
///     <see cref="IBatchScope" /> opt-in, and registers the scoped keyed <see cref="DbConnection" />
///     (keyed <c>nameof(ConnectionProvider)</c>) as batch-only: it always connects as <c>booqr_batch</c>.
///     There is no fallback to a tenant connection.
/// </summary>
/// <remarks>
///     <para>
///         <b>Call order:</b> call <see cref="AddBatchDataSource" /> only from the worker composition
///         root. It registers the keyed <see cref="DbConnection" /> service unconditionally as the
///         batch connection, so it must never be composed into the same service collection as
///         <c>AddTenantDataSources</c> (the API's <c>AddInfrastructure</c>) - doing so would make every
///         resolution of that keyed connection bypass RLS.
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
///         resolving any repository from that scope, for intent/correctness signalling. Never enable
///         this for a request-handling scope.
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

        // Registers the scoped keyed DbConnection as batch-only: always connects as booqr_batch
        // (BYPASSRLS, cross-tenant). No fallback to a tenant connection - this process never holds a
        // tenant data source or tenant master secret.
        services.AddKeyedScoped<DbConnection>(nameof(ConnectionProvider), static (serviceProvider, _) =>
        {
            NpgsqlDataSource batchDataSource =
                serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(BatchDataSourceKey);
            return batchDataSource.CreateConnection();
        });

        return services;
    }

    [LoggerMessage(1060, LogLevel.Information, "Batch (BYPASSRLS) connection is at {Host}")]
    private static partial void Batch(ILogger logger, string? host);
}
