using System.Data.Common;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Dapper;
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

    // Deterministic per-role test password for provisioned tenant roles (t_<id>). Not derived via
    // the HMAC master-secret scheme (that's the API's Phase 2 concern) — the fixture provisions
    // tenants directly, mirroring only the DDL steps of `admin --provision`.
    private const string TenantRolePassword = "changeme_tenant_role";

    private ServiceProvider? _services;

    internal IServiceProvider Services => _services!;

    /// <summary>Id of the first provisioned tenant (also the default DI connection's tenant).</summary>
    internal int TenantAId { get; private set; }

    /// <summary>Id of the second provisioned tenant, used by isolation tests.</summary>
    internal int TenantBId { get; private set; }

    private PostgreSqlContainer SqlContainer { get; } =
        new PostgreSqlBuilder("postgres:18-alpine3.23")
        .Build();

    private static string TenantRole(int tenantId) => $"t_{tenantId}";

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
                { nameof(InfrastructureSettings.ConnectionString), TenantConnectionString(TenantAId) },
                { nameof(InfrastructureSettings.MailClientApiKey), settings.MailClientApiKey },
                { nameof(InfrastructureSettings.MailClientAccount), settings.MailClientAccount },
                { nameof(InfrastructureSettings.MailClientFromAddress), settings.MailClientFromAddress },
                { nameof(InfrastructureSettings.MailClientBaseAddress), settings.MailClientBaseAddress.ToString() },
            })
            .Build();
        _services = new ServiceCollection()
            .AddSingleton<TimeProvider, FakeTimeProvider>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddInfrastructure(config)
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

    private string TenantConnectionString(int tenantId)
    {
        NpgsqlConnectionStringBuilder builder = new(SqlContainer.GetConnectionString())
        {
            Username = TenantRole(tenantId),
            Password = TenantRolePassword,
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
        await connection.ExecuteAsync(
            $"""create role "{role}" login password '{TenantRolePassword}' nobypassrls""");
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
