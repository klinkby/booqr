using System.Data.Common;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Dapper;
using Klinkby.Booqr.Core;
using Klinkby.Booqr.Infrastructure.Models;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

[module: DapperAot]

namespace Klinkby.Booqr.Infrastructure.Tests;

/// <summary>
///     Builds the test schema the same way production does: run the initdb control-plane SQL as
///     the Postgres superuser, then run <see cref="SchemaMigrator" /> (as <c>booqr_migrator</c>)
///     to build schema <c>app</c>, then provision two tenant roles. The default DI-registered
///     connection (<see cref="IConnectionProvider" />) connects as tenant A with
///     <c>search_path=app</c>, so existing repository tests keep working against the tenant-scoped
///     `app` tables. Tests that specifically exercise RLS isolation should open a connection for
///     the tenant of their choice via <see cref="OpenTenantConnection" />.
/// </summary>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public sealed class ServiceProviderFixture : IAsyncLifetime
{
    // Deterministic placeholder passwords from redist/initdb/01-control-plane.sql. Production
    // deployments rotate these; tests connect with the same fixed values initdb creates the roles
    // with, so there's nothing to coordinate out-of-band.
    private const string MigratorPassword = "changeme_booqr_migrator";

    // Deterministic placeholder password for booqr_registry from redist/initdb/01-control-plane.sql
    // (see MigratorPassword above for the same rationale).
    private const string RegistryPassword = "changeme_booqr_registry";

    // Deterministic placeholder password for booqr_batch (BYPASSRLS) from
    // redist/initdb/01-control-plane.sql (see MigratorPassword above for the same rationale).
    private const string BatchPassword = "changeme_booqr_batch";

    // Fixed test master secret. Provisioned tenant role (t_<id>) passwords are derived from this via
    // TenantCredentials.DerivePassword — the same algorithm TenantDataSourceFactory uses — so the
    // fixture's provisioned roles match what the DI-registered factory (and future admin CLI
    // provisioning) derives.
    private const string TenantMasterSecret = "changeme_tenant_master_secret";

    private ServiceProvider? _services;

    internal IServiceProvider Services => _services!;

    /// <summary>Id of the first provisioned tenant (also the default DI connection's tenant).</summary>
    internal int TenantAId { get; private set; }

    /// <summary>Id of the second provisioned tenant, used by isolation tests.</summary>
    internal int TenantBId { get; private set; }

    /// <summary>The fixed test master secret used to provision tenant role passwords (see <see cref="TenantMasterSecret" />).</summary>
    internal static string MasterSecret => TenantMasterSecret;

    /// <summary>
    ///     The base connection string (host/port/database only — no <c>Username</c>/<c>Password</c>),
    ///     for tests constructing a <see cref="TenantDataSourceFactory" /> directly.
    /// </summary>
    internal string BaseConnectionString => SqlContainer.GetConnectionString();

    private PostgreSqlContainer SqlContainer { get; } =
        new PostgreSqlBuilder("postgres:18-alpine3.23")
        .Build();

    internal static string TenantRole(int tenantId) => $"t_{tenantId}";

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await SqlContainer.StartAsync(TestContext.Current.CancellationToken);

        await ApplyControlPlane(TestContext.Current.CancellationToken);
        await RunMigrations(TestContext.Current.CancellationToken);
        TenantAId = await ProvisionTenant("tenant-a", TestContext.Current.CancellationToken);
        TenantBId = await ProvisionTenant("tenant-b", TestContext.Current.CancellationToken);

        Fixture fixture = new();
        InfrastructureSettings? settings = fixture.Create<InfrastructureSettings>();
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { nameof(InfrastructureSettings.MailClientApiKey), settings.MailClientApiKey },
                { nameof(InfrastructureSettings.MailClientAccount), settings.MailClientAccount },
                { nameof(InfrastructureSettings.MailClientFromAddress), settings.MailClientFromAddress },
                { nameof(InfrastructureSettings.MailClientBaseAddress), settings.MailClientBaseAddress.ToString() },
                // Tenancy configuration (Phase 2c): base domain and reserved subdomains.
                { "Tenancy:BaseDomain", "booqr.dk" },
                { "Tenancy:ReservedSubdomains:0", "www" },
                { "Tenancy:ReservedSubdomains:1", "status" },
                { "Tenancy:ReservedSubdomains:2", "mta-sts" },
                // Tenant data-source factory configuration (Phase 2a): base connection, master secret, pool/cache config.
                // BaseConnectionString must be stripped of credentials (host/database only); the factory adds the per-tenant role/password.
                { "TenantDataSources:BaseConnectionString", SqlContainer.GetConnectionString() },
                { "TenantDataSources:MasterSecret", TenantMasterSecret },
                { "TenantDataSources:MaxPoolSize", "3" },
                { "TenantDataSources:MaxCacheEntries", "64" },
                // Registry connection (booqr_registry, SELECT-only on public.tenants) — separate
                // from the tenant connection above. See Services/RegistryServiceCollectionExtensions.cs
                // (subtask 2b) for the config keys this binds.
                { "Registry:ConnectionString", SqlContainer.GetConnectionString() },
                { "Registry:RegistryUsername", "booqr_registry" },
                { "Registry:RegistryPassword", RegistryPassword },
                // Cross-tenant booqr_batch (BYPASSRLS) connection (Phase 3c). See
                // Services/BatchServiceCollectionExtensions.cs for the config keys this binds.
                { "Batch:ConnectionString", SqlContainer.GetConnectionString() },
                { "Batch:BatchUsername", "booqr_batch" },
                { "Batch:BatchPassword", BatchPassword },
            })
            .Build();
        _services = new ServiceCollection()
            .AddSingleton<TimeProvider, FakeTimeProvider>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddApiInfrastructure(config)
            // Register a test ITenantContext that resolves to tenant A by default.
            // This allows existing repository tests to work without modification;
            // they will connect as tenant A (the default). Tests that need to verify
            // multi-tenant isolation can open separate connections via OpenTenantConnection().
            .AddScoped<ITenantContext>(_ => new TestTenantContext(TenantAId))
            .BuildServiceProvider();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await SqlContainer.DisposeAsync();
        await _services!.DisposeAsync();
    }

    /// <summary>
    ///     Opens a fresh connection as the given tenant role (<c>search_path=app</c>), separate
    ///     from the DI-registered scoped connection. Used by RLS isolation tests that need to
    ///     compare two tenants' views of the same tables side by side.
    /// </summary>
    internal async Task<NpgsqlConnection> OpenTenantConnection(int tenantId, CancellationToken cancellation)
    {
        NpgsqlConnection connection = new(TenantConnectionString(tenantId));
        await connection.OpenAsync(cancellation);
        return connection;
    }

    /// <summary>
    ///     Opens a fresh connection as <c>booqr_migrator</c>, which holds INSERT/UPDATE on
    ///     <c>public.tenants</c> (see redist/initdb/01-control-plane.sql). Used by tests that need
    ///     to mutate the registry directly (e.g. asserting cache-TTL behavior around a newly
    ///     inserted or deleted tenant row) without going through the read-only registry role.
    /// </summary>
    internal async Task<NpgsqlConnection> OpenMigratorConnection(CancellationToken cancellation)
    {
        NpgsqlConnection connection = new(MigratorConnectionString());
        await connection.OpenAsync(cancellation);
        return connection;
    }

    /// <summary>
    ///     Builds an <see cref="NpgsqlDataSource" /> connected as <c>booqr_migrator</c>. Used by tests
    ///     exercising <c>Klinkby.Booqr.Control.TenantProvisioner</c> directly, which (like
    ///     <see cref="SchemaMigrator" />) takes an already-built migrator data source rather than
    ///     constructing its own.
    /// </summary>
    internal NpgsqlDataSource CreateMigratorDataSource() => NpgsqlDataSource.Create(MigratorConnectionString());

    /// <summary>
    ///     Opens a fresh connection as <c>booqr_batch</c> (BYPASSRLS), which bypasses row-level
    ///     security and sees all tenants' rows across the entire database. Used by tests that verify
    ///     cross-tenant behavior for background/batch work and assert that regular tenant roles
    ///     cannot perform such queries. The batch role has the full booqr_tenant grant set via
    ///     default privileges in redist/initdb/02-provisioning-engine.sql.
    /// </summary>
    internal async Task<NpgsqlConnection> OpenBatchConnection(CancellationToken cancellation)
    {
        NpgsqlConnection connection = new(BatchConnectionString());
        await connection.OpenAsync(cancellation);
        return connection;
    }

    private string TenantConnectionString(int tenantId)
    {
        NpgsqlConnectionStringBuilder builder = new(SqlContainer.GetConnectionString())
        {
            Username = TenantRole(tenantId),
            Password = TenantCredentials.DerivePassword(TenantMasterSecret, tenantId),
        };
        return builder.ConnectionString;
    }

    private string MigratorConnectionString()
    {
        NpgsqlConnectionStringBuilder builder = new(SqlContainer.GetConnectionString())
        {
            Username = "booqr_migrator",
            Password = MigratorPassword,
        };
        return builder.ConnectionString;
    }

    private string BatchConnectionString()
    {
        NpgsqlConnectionStringBuilder builder = new(SqlContainer.GetConnectionString())
        {
            Username = "booqr_batch",
            Password = BatchPassword,
        };
        return builder.ConnectionString;
    }

    private async Task ApplyControlPlane(CancellationToken cancellation)
    {
        await using NpgsqlConnection connection = new(SqlContainer.GetConnectionString());
        await connection.OpenAsync(cancellation);

        foreach (var resourceName in new[]
                 {
                     "Klinkby.Booqr.Infrastructure.Tests.initdb.01-control-plane.sql",
                     "Klinkby.Booqr.Infrastructure.Tests.initdb.02-provisioning-engine.sql",
                 })
        {
            using StreamReader sr = new(
                typeof(ServiceProviderFixture).Assembly.GetManifestResourceStream(resourceName)!);
            var script = await sr.ReadToEndAsync(cancellation);
            await connection.ExecuteAsync(script);
        }
    }

    private async Task RunMigrations(CancellationToken cancellation)
    {
        await using NpgsqlDataSource migratorDataSource =
            NpgsqlDataSource.Create(MigratorConnectionString());
        SchemaMigrator migrator = new(migratorDataSource);
        await migrator.Migrate(cancellation);
    }

    private async Task<int> ProvisionTenant(string slug, CancellationToken cancellation)
    {
        await using NpgsqlConnection connection = new(SqlContainer.GetConnectionString());
        await connection.OpenAsync(cancellation);

        // db_role depends on the generated id, so insert a placeholder and update it once the id
        // is known.
        var tenantId = await connection.ExecuteScalarAsync<int>(
            """
            insert into public.tenants (slug, db_role, display_name)
            values (@slug, 'pending', @slug)
            returning id
            """, new { slug });

        await connection.ExecuteAsync(
            "update public.tenants set db_role = @dbRole where id = @id",
            new { dbRole = TenantRole(tenantId), id = tenantId });

        var role = TenantRole(tenantId);
        var password = TenantCredentials.DerivePassword(TenantMasterSecret, tenantId);
        await connection.ExecuteAsync(
            $"""create role "{role}" login password '{password}' nobypassrls""");
        await connection.ExecuteAsync($"""grant booqr_tenant to "{role}" """);
        await connection.ExecuteAsync($"""alter role "{role}" set search_path = app""");

        return tenantId;
    }
}

[CollectionDefinition(nameof(ServiceProviderFixture))]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public class ServiceProviderCollectionFixture : ICollectionFixture<ServiceProviderFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
