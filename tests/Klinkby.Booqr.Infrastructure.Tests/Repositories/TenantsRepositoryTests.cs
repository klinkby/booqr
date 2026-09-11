using Dapper;
using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

/// <summary>
///     Exercises <see cref="ITenantRepository" /> (registry data source + bounded-TTL cache) against
///     the two tenants the fixture provisions at startup (docs/1-design.md §2a "Registry data
///     source"; §5 "deleted tenant stops resolving within the registry cache TTL").
/// </summary>
[Collection(nameof(ServiceProviderFixture))]
public sealed class TenantsRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly ITenantRepository _sut = serviceProvider.Services.GetRequiredService<ITenantRepository>();

    [Fact]
    public async Task GIVEN_ValidSlug_WHEN_GetBySlug_THEN_ReturnsMatchingTenant()
    {
        Tenant? tenant = await _sut.GetBySlug("tenant-a", CancellationToken.None);

        Assert.NotNull(tenant);
        Assert.Equal(serviceProvider.TenantAId, tenant.Id);
        Assert.Equal("tenant-a", tenant.Slug);
        Assert.Equal($"t_{serviceProvider.TenantAId}", tenant.DbRole);
    }

    [Fact]
    public async Task GIVEN_UnknownSlug_WHEN_GetBySlug_THEN_ReturnsNull()
    {
        Tenant? tenant = await _sut.GetBySlug($"nonexistent-{Guid.NewGuid():N}", CancellationToken.None);

        Assert.Null(tenant);
    }

    [Fact]
    public async Task GIVEN_NotFoundResultIsCached_WHEN_TtlHasNotElapsed_THEN_StaleCachedResultIsReturned()
    {
        // The FakeTimeProvider registered for the SUT's DI graph is shared across the fixture, so
        // advancing it here affects the cache's clock deterministically without touching wall time.
        var timeProvider = (FakeTimeProvider)serviceProvider.Services.GetRequiredService<TimeProvider>();
        // slug is varchar(32); keep the generated value within that bound.
        var slug = $"tc-{Guid.NewGuid():N}"[..32];

        Tenant? beforeInsert = await _sut.GetBySlug(slug, CancellationToken.None);
        Assert.Null(beforeInsert);

        // Insert a tenant with this slug directly (as booqr_migrator); the not-found result is
        // still cached, so a second lookup within the TTL must still report null.
        await using (NpgsqlConnection connection =
                      await serviceProvider.OpenMigratorConnection(CancellationToken.None))
        {
            try
            {
                await connection.ExecuteAsync(
                    "insert into public.tenants (slug, db_role, display_name) values (@slug, 'pending', @slug)",
                    new { slug });

                Tenant? stillCachedAsNull = await _sut.GetBySlug(slug, CancellationToken.None);
                Assert.Null(stillCachedAsNull);

                // Advance past the configured TTL (default 30s from RegistrySettings) so the
                // negative cache entry expires and the now-existing row resolves.
                timeProvider.Advance(TimeSpan.FromSeconds(31));

                Tenant? resolvedAfterTtl = await _sut.GetBySlug(slug, CancellationToken.None);
                Assert.NotNull(resolvedAfterTtl);
                Assert.Equal(slug, resolvedAfterTtl.Slug);
            }
            finally
            {
                // Soft-delete, mirroring the real deprovision path: booqr_migrator holds
                // SELECT/INSERT/UPDATE on public.tenants but not DELETE (least privilege), and
                // GetBySlug filters WHERE deleted IS NULL, so this both cleans up and matches prod.
                await connection.ExecuteAsync(
                    "update public.tenants set deleted = now() where slug = @slug", new { slug });
            }
        }
    }
}
