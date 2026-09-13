using Dapper;
using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

/// <summary>
///     Smoke tests for the row-level-security tenant isolation mechanism described in
///     docs/1-design.md ("The RLS mechanism"): connected as a tenant role, queries only see that
///     tenant's rows, and a row cannot be stamped with (or moved to) another tenant's id.
/// </summary>
[Collection(nameof(ServiceProviderFixture))]
public sealed class RlsIsolationTests(ServiceProviderFixture serviceProvider)
{
    [Fact]
    public async Task GIVEN_TwoTenantsWithRows_WHEN_SelectingAsTenantA_THEN_OnlyTenantARowsAreVisible()
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
                "insert into locations (name, created, modified) values (@name, now(), now())",
                new { name = nameA });
            await connectionB.ExecuteAsync(
                "insert into locations (name, created, modified) values (@name, now(), now())",
                new { name = nameB });

            IEnumerable<string> visibleToA = await connectionA.QueryAsync<string>(
                "select name from locations where name in (@nameA, @nameB)", new { nameA, nameB });

            Assert.Contains(nameA, visibleToA);
            Assert.DoesNotContain(nameB, visibleToA);
        }
        finally
        {
            await connectionA.ExecuteAsync("delete from locations where name = @name", new { name = nameA });
            await connectionB.ExecuteAsync("delete from locations where name = @name", new { name = nameB });
        }
    }

    [Fact]
    public async Task GIVEN_TenantARole_WHEN_InsertingWithAnotherTenantsId_THEN_ViolatesRowSecurityPolicy()
    {
        await using NpgsqlConnection connectionA =
            await serviceProvider.OpenTenantConnection(serviceProvider.TenantAId, CancellationToken.None);

        var name = $"loc-forged-{Guid.NewGuid():N}";

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
            connectionA.ExecuteAsync(
                "insert into locations (tenant_id, name, created, modified) values (@tenantId, @name, now(), now())",
                new { tenantId = serviceProvider.TenantBId, name }));

        Assert.Equal("42501", ex.SqlState); // insufficient_privilege: row-security policy violation
    }

    [Fact]
    public async Task GIVEN_TenantARow_WHEN_UpdatingTenantIdToAnotherTenant_THEN_ViolatesRowSecurityPolicy()
    {
        await using NpgsqlConnection connectionA =
            await serviceProvider.OpenTenantConnection(serviceProvider.TenantAId, CancellationToken.None);

        var name = $"loc-update-{Guid.NewGuid():N}";
        await connectionA.ExecuteAsync(
            "insert into locations (name, created, modified) values (@name, now(), now())", new { name });
        try
        {
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
                connectionA.ExecuteAsync(
                    "update locations set tenant_id = @tenantId where name = @name",
                    new { tenantId = serviceProvider.TenantBId, name }));

            Assert.Equal("42501", ex.SqlState);
        }
        finally
        {
            await connectionA.ExecuteAsync("delete from locations where name = @name", new { name });
        }
    }
}
