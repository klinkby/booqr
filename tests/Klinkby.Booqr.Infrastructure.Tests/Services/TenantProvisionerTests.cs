using Klinkby.Booqr.Control;
using Klinkby.Booqr.Infrastructure.Services;
using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Tests.Services;

/// <summary>
///     Integration tests for <see cref="TenantProvisioner" /> (Phase 5 admin CLI provisioning),
///     exercised directly against the Testcontainers fixture rather than through
///     <c>Klinkby.Booqr.Control.AdminRunner</c>'s process-args/env-var seam — see
///     <c>docs/2-implementation.md</c> Phase 5, "If AdminRunner's static seams make testing hard,
///     refactor into an injectable class".
/// </summary>
[Collection(nameof(ServiceProviderFixture))]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Test-only role name is derived from a Postgres sequence value (an integer), never user input.")]
public sealed class TenantProvisionerTests(ServiceProviderFixture fixture)
{
    private TenantProvisioner CreateProvisioner(NpgsqlDataSource migratorDataSource) =>
        new(migratorDataSource, fixture.BaseConnectionString, ServiceProviderFixture.MasterSecret);

    private static string NewSlug(string prefix)
    {
        // Slug must match ^[a-z0-9]([a-z0-9-]{0,30}[a-z0-9])?$ (max 32 chars, alnum start/end).
        var candidate = $"{prefix}-{Guid.NewGuid():N}";
        return candidate.Length > 32 ? candidate[..32] : candidate;
    }

    [Fact]
    public async Task GIVEN_ValidSlug_WHEN_Provisioning_THEN_CreatesRegistryRowRoleAndSeedAdmin()
    {
        var slug = NewSlug("prov");
        var seedEmail = $"admin@{slug}.booqr.dk";

        await using NpgsqlDataSource migratorDataSource = fixture.CreateMigratorDataSource();
        TenantProvisioner sut = CreateProvisioner(migratorDataSource);

        ProvisionResult result = await sut.Provision(slug, slug, seedEmail, TestContext.Current.CancellationToken);

        try
        {
            Assert.True(result.TenantId > 0);
            Assert.Equal($"t_{result.TenantId}", result.DbRole);
            Assert.True(result.SeedAdminUserId > 0);
            Assert.Equal(seedEmail, result.SeedAdminEmail);

            // Registry row exists with the expected db_role.
            await using NpgsqlConnection migratorConnection =
                await fixture.OpenMigratorConnection(TestContext.Current.CancellationToken);
            await using NpgsqlCommand select = new(
                "select db_role from public.tenants where id = $1 and deleted is null", migratorConnection);
            select.Parameters.Add(new NpgsqlParameter<int> { TypedValue = result.TenantId });
            var dbRole = (string?)await select.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            Assert.Equal(result.DbRole, dbRole);

            // The seed admin user is visible when connected as the tenant role (RLS-scoped), has the
            // Admin role, and a null passwordhash (activation pending).
            await using NpgsqlConnection tenantConnection =
                await fixture.OpenTenantConnection(result.TenantId, TestContext.Current.CancellationToken);
            await using NpgsqlCommand selectUser = new(
                "select role, passwordhash from users where id = $1", tenantConnection);
            selectUser.Parameters.Add(new NpgsqlParameter<int> { TypedValue = result.SeedAdminUserId });
            await using NpgsqlDataReader reader =
                await selectUser.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(UserRole.Admin, reader.GetString(0));
            Assert.True(await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken));
        }
        finally
        {
            await using NpgsqlDataSource cleanupDataSource = fixture.CreateMigratorDataSource();
            TenantProvisioner cleanup = CreateProvisioner(cleanupDataSource);
            await cleanup.Deprovision(result.TenantId, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GIVEN_RoleAlreadyExists_WHEN_Provisioning_THEN_RefusesAndDoesNotAdoptOrphan()
    {
        var orphanSlug = NewSlug("orph");

        await using NpgsqlDataSource migratorDataSource = fixture.CreateMigratorDataSource();
        TenantProvisioner sut = CreateProvisioner(migratorDataSource);

        // Provision a real tenant to consume an id, then pre-create the role for the NEXT id — the
        // one the following Provision call will target — simulating an orphaned role left behind by a
        // prior failed/incomplete provisioning. Identity ids are sequential in this single-threaded
        // test, so the next id is result.TenantId + 1. The orphan role is created as booqr_migrator
        // (which has CREATEROLE), avoiding any need to read the identity sequence directly.
        ProvisionResult seed = await sut.Provision(
            NewSlug("orph-seed"), null, $"seed@{orphanSlug}.booqr.dk", TestContext.Current.CancellationToken);
        var nextId = seed.TenantId + 1;
        await using (NpgsqlConnection conn = await fixture.OpenMigratorConnection(TestContext.Current.CancellationToken))
        {
            await using NpgsqlCommand createOrphan = new(
                $"""create role "t_{nextId}" login password 'orphan' nobypassrls""", conn);
            await createOrphan.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        try
        {
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.Provision(orphanSlug, orphanSlug, $"a@{orphanSlug}.booqr.dk", TestContext.Current.CancellationToken));
            Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);

            // The tenants row inserted before the role check must have been rolled back — the slug
            // must not be left dangling in the registry.
            await using NpgsqlConnection verifyConnection =
                await fixture.OpenMigratorConnection(TestContext.Current.CancellationToken);
            await using NpgsqlCommand select = new(
                "select count(*) from public.tenants where slug = $1", verifyConnection);
            select.Parameters.Add(new NpgsqlParameter<string> { TypedValue = orphanSlug });
            var count = (long)(await select.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
            Assert.Equal(0, count);
        }
        finally
        {
            await using NpgsqlConnection conn = await fixture.OpenMigratorConnection(TestContext.Current.CancellationToken);
            await using NpgsqlCommand dropOrphan = new($"""drop role "t_{nextId}" """, conn);
            await dropOrphan.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GIVEN_DeprovisionedTenant_WHEN_SlugReprovisioned_THEN_GetsNewIdAndRole()
    {
        var slug = NewSlug("reuse");

        await using NpgsqlDataSource migratorDataSource = fixture.CreateMigratorDataSource();
        TenantProvisioner sut = CreateProvisioner(migratorDataSource);

        ProvisionResult first = await sut.Provision(slug, slug, $"a@{slug}.booqr.dk", TestContext.Current.CancellationToken);
        DeprovisionOutcome deprovisionOutcome = await sut.Deprovision(first.TenantId, TestContext.Current.CancellationToken);
        Assert.Equal(DeprovisionOutcome.Deprovisioned, deprovisionOutcome);

        ProvisionResult second = await sut.Provision(slug, slug, $"c@{slug}.booqr.dk", TestContext.Current.CancellationToken);
        try
        {
            Assert.NotEqual(first.TenantId, second.TenantId);
            Assert.NotEqual(first.DbRole, second.DbRole);
        }
        finally
        {
            await sut.Deprovision(second.TenantId, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GIVEN_ProvisionedTenant_WHEN_Deprovisioning_THEN_SoftDeletesAndTenantStopsResolving()
    {
        var slug = NewSlug("depr");

        await using NpgsqlDataSource migratorDataSource = fixture.CreateMigratorDataSource();
        TenantProvisioner sut = CreateProvisioner(migratorDataSource);

        ProvisionResult result = await sut.Provision(slug, slug, $"a@{slug}.booqr.dk", TestContext.Current.CancellationToken);

        DeprovisionOutcome outcome = await sut.Deprovision(result.TenantId, TestContext.Current.CancellationToken);
        Assert.Equal(DeprovisionOutcome.Deprovisioned, outcome);

        // Registry no longer resolves this tenant as active (deleted IS NOT NULL).
        await using NpgsqlConnection migratorConnection =
            await fixture.OpenMigratorConnection(TestContext.Current.CancellationToken);
        await using NpgsqlCommand select = new(
            "select deleted from public.tenants where id = $1", migratorConnection);
        select.Parameters.Add(new NpgsqlParameter<int> { TypedValue = result.TenantId });
        var deleted = await select.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(deleted);

        // Repeating the deprovision is reported as NotFound (already soft-deleted), not an error.
        DeprovisionOutcome repeatedOutcome =
            await sut.Deprovision(result.TenantId, TestContext.Current.CancellationToken);
        Assert.Equal(DeprovisionOutcome.NotFound, repeatedOutcome);
    }
}
