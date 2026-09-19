using Dapper;
using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

/// <summary>
///     Integration tests proving the <c>booqr_batch</c> (BYPASSRLS) cross-tenant behavior as
///     required by docs/1-design.md §5: background jobs must see all tenants' rows, while
///     regular tenant roles remain confined to their own data by RLS. These tests contrast
///     the behavior of the batch connection against tenant-scoped connections.
/// </summary>
[Collection(nameof(ServiceProviderFixture))]
public sealed class BatchIsolationTests(ServiceProviderFixture serviceProvider)
{
    /// <summary>
    ///     GIVEN two tenants, each with a row in app.locations, WHEN querying as the batch
    ///     connection (BYPASSRLS), THEN both tenants' rows are visible in a single query.
    ///     This proves batch can perform cross-tenant operations for background work (e.g.,
    ///     reminder mail, token flush).
    /// </summary>
    [Fact]
    public async Task GIVEN_TwoTenantsWithRows_WHEN_SelectingAsBatch_THEN_AllTenantsRowsAreVisible()
    {
        await using NpgsqlConnection connectionA =
            await serviceProvider.OpenTenantConnection(serviceProvider.TenantAId, CancellationToken.None);
        await using NpgsqlConnection connectionB =
            await serviceProvider.OpenTenantConnection(serviceProvider.TenantBId, CancellationToken.None);
        await using NpgsqlConnection batchConnection =
            await serviceProvider.OpenBatchConnection(CancellationToken.None);

        var nameA = $"loc-a-{Guid.NewGuid():N}";
        var nameB = $"loc-b-{Guid.NewGuid():N}";
        try
        {
            // Each tenant inserts a row; tenant roles do NOT provide tenant_id, relying on the
            // DEFAULT (app.tenant_of(current_user)) to stamp the row with their tenant id.
            await connectionA.ExecuteAsync(
                "insert into app.locations (name, created, modified) values (@name, now(), now())",
                new { name = nameA });
            await connectionB.ExecuteAsync(
                "insert into app.locations (name, created, modified) values (@name, now(), now())",
                new { name = nameB });

            // The batch connection (BYPASSRLS) selects both rows in one query, seeing across tenants.
            IEnumerable<string> visibleToBatch = await batchConnection.QueryAsync<string>(
                "select name from app.locations where name in (@nameA, @nameB)", new { nameA, nameB });

            // Assert both rows are visible to the batch connection.
            var list = visibleToBatch.ToList();
            Assert.Contains(nameA, list);
            Assert.Contains(nameB, list);
            Assert.Equal(2, list.Count);
        }
        finally
        {
            // Cleanup: batch role (BYPASSRLS) can delete across tenants; no row-level restriction.
            await batchConnection.ExecuteAsync("delete from app.locations where name in (@nameA, @nameB)",
                new { nameA, nameB });
        }
    }

    /// <summary>
    ///     GIVEN two tenants, each with a row in app.locations, WHEN querying as tenant A,
    ///     THEN only tenant A's row is visible; tenant B's row is NOT visible due to RLS.
    ///     This proves that non-BYPASSRLS tenant roles remain confined to their own data and
    ///     cannot perform cross-tenant queries, contrasting with the batch role's BYPASSRLS behavior.
    /// </summary>
    [Fact]
    public async Task GIVEN_TwoTenantsWithRows_WHEN_SelectingAsTenantA_THEN_TenantBRowsAreNotVisible()
    {
        await using NpgsqlConnection connectionA =
            await serviceProvider.OpenTenantConnection(serviceProvider.TenantAId, CancellationToken.None);
        await using NpgsqlConnection connectionB =
            await serviceProvider.OpenTenantConnection(serviceProvider.TenantBId, CancellationToken.None);

        var nameA = $"loc-a-{Guid.NewGuid():N}";
        var nameB = $"loc-b-{Guid.NewGuid():N}";
        try
        {
            await connectionA.ExecuteAsync(
                "insert into app.locations (name, created, modified) values (@name, now(), now())",
                new { name = nameA });
            await connectionB.ExecuteAsync(
                "insert into app.locations (name, created, modified) values (@name, now(), now())",
                new { name = nameB });

            // Tenant A queries for both rows, but RLS restricts it to rows where tenant_id matches.
            IEnumerable<string> visibleToA = await connectionA.QueryAsync<string>(
                "select name from app.locations where name in (@nameA, @nameB)", new { nameA, nameB });

            // Assert only tenant A's row is visible; tenant B's row is filtered by RLS policy.
            var list = visibleToA.ToList();
            Assert.Contains(nameA, list);
            Assert.DoesNotContain(nameB, list);
            Assert.Single(list);
        }
        finally
        {
            // Each tenant cleans up its own row (and cannot delete the other's due to RLS).
            await connectionA.ExecuteAsync("delete from app.locations where name = @name", new { name = nameA });
            await connectionB.ExecuteAsync("delete from app.locations where name = @name", new { name = nameB });
        }
    }
}
