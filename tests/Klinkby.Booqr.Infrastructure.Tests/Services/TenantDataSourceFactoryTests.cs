using Dapper;
using Klinkby.Booqr.Infrastructure.Services;
using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Tests.Services;

/// <summary>
///     Proves the tenant data-source factory (a) derives a password that actually authenticates
///     against the provisioned <c>t_&lt;id&gt;</c> role, and (b) that RLS confines the resulting
///     connection to that tenant's own rows only.
/// </summary>
[Collection(nameof(ServiceProviderFixture))]
public sealed class TenantDataSourceFactoryTests(ServiceProviderFixture serviceProvider)
{
    [Fact]
    public async Task GIVEN_ProvisionedTenant_WHEN_AcquiringDataSource_THEN_DerivedPasswordAuthenticates()
    {
        using TenantDataSourceFactory sut = new(
            serviceProvider.BaseConnectionString,
            ServiceProviderFixture.MasterSecret,
            maxPoolSize: 3,
            maxEntries: 8);

        var tenantId = serviceProvider.TenantAId;
        using TenantDataSourceLease lease = sut.Acquire(tenantId);

        await using NpgsqlConnection connection = await lease.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var currentUser = await connection.ExecuteScalarAsync<string?>("select current_user");

        Assert.Equal(ServiceProviderFixture.TenantRole(tenantId), currentUser);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_TwoTenants_WHEN_QueryingViaFactoryConnection_THEN_SeesOnlyOwnTenantRows(Location location)
    {
        using TenantDataSourceFactory sut = new(
            serviceProvider.BaseConnectionString,
            ServiceProviderFixture.MasterSecret,
            maxPoolSize: 3,
            maxEntries: 8);

        ILocationRepository locations = serviceProvider.Services.GetRequiredService<ILocationRepository>();
        ITransaction transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

        // The DI-registered connection (from ServiceProviderFixture) is tenant A. Seed one row
        // there (committed, so it's visible on a separate connection), then open tenant B via the
        // factory and prove it cannot see tenant A's row (RLS), while tenant A (via the factory) can
        // see its own.
        await transaction.Begin(TestContext.Current.CancellationToken);
        var locationId = await locations.Add(location);
        await transaction.Commit(TestContext.Current.CancellationToken);

        try
        {
            using TenantDataSourceLease leaseA = sut.Acquire(serviceProvider.TenantAId);
            using TenantDataSourceLease leaseB = sut.Acquire(serviceProvider.TenantBId);

            await using NpgsqlConnection connectionA = await leaseA.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using NpgsqlConnection connectionB = await leaseB.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);

            var seenByA = await connectionA.ExecuteScalarAsync<int?>(
                "select id from locations where id = @id", new { id = locationId });
            var seenByB = await connectionB.ExecuteScalarAsync<int?>(
                "select id from locations where id = @id", new { id = locationId });

            Assert.Equal(locationId, seenByA);
            Assert.Null(seenByB);
        }
        finally
        {
            await transaction.Begin(TestContext.Current.CancellationToken);
            await locations.Delete(locationId);
            await transaction.Commit(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GIVEN_SameTenantAcquiredTwice_WHEN_ReleasingOneLease_THEN_DataSourceStillUsableViaOtherLease()
    {
        using TenantDataSourceFactory sut = new(
            serviceProvider.BaseConnectionString,
            ServiceProviderFixture.MasterSecret,
            maxPoolSize: 3,
            maxEntries: 8);

        var tenantId = serviceProvider.TenantAId;
        var role = ServiceProviderFixture.TenantRole(tenantId);

        TenantDataSourceLease first = sut.Acquire(tenantId);
        TenantDataSourceLease second = sut.Acquire(tenantId);

        Assert.Same(first.DataSource, second.DataSource);

        // Releasing the first lease must not dispose the shared data source while the second lease
        // is still outstanding.
        first.Dispose();

        await using NpgsqlConnection connection = await second.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var currentUser = await connection.ExecuteScalarAsync<string>("select current_user");
        Assert.Equal(role, currentUser);

        second.Dispose();
    }
}
