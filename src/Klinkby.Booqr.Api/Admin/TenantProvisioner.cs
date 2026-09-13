using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Klinkby.Booqr.Infrastructure.Services;
using Npgsql;

namespace Klinkby.Booqr.Api.Admin;

/// <summary>
///     The DNS-label slug validator shared by <see cref="TenantProvisioner.Provision" /> — a public
///     slug is routing input and is validated before it ever reaches SQL (it is used only as a
///     parameterized value, never interpolated into DDL/identifiers). Per
///     <c>docs/1-design.md</c> "Naming safety".
/// </summary>
public static partial class SlugValidator
{
    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,30}[a-z0-9])?$")]
    internal static partial Regex Pattern();

    public static bool IsValid(string? slug) => slug is not null && Pattern().IsMatch(slug);
}

/// <summary>Result of a successful <see cref="TenantProvisioner.Provision" /> call.</summary>
/// <param name="TenantId">The newly assigned immutable tenant id.</param>
/// <param name="Slug">The tenant's public slug.</param>
/// <param name="DbRole">The tenant's PostgreSQL login role (<c>t_&lt;id&gt;</c>).</param>
/// <param name="SeedAdminUserId">The id of the seeded initial Admin user row.</param>
/// <param name="SeedAdminEmail">The email address of the seeded initial Admin user.</param>
public sealed record ProvisionResult(int TenantId, string Slug, string DbRole, int SeedAdminUserId, string SeedAdminEmail);

/// <summary>Outcome of a <see cref="TenantProvisioner.Deprovision" /> call.</summary>
public enum DeprovisionOutcome
{
    /// <summary>Tenant row soft-deleted and its role dropped.</summary>
    Deprovisioned,

    /// <summary>Tenant row soft-deleted; the role had already been dropped (no-op, not an error).</summary>
    DeprovisionedRoleAlreadyDropped,

    /// <summary>No non-deleted tenant with the given id exists.</summary>
    NotFound
}

/// <summary>
///     Implements the provisioning/deprovision/rotation DDL sequences from
///     <c>docs/1-design.md</c> "## 2b. Admin CLI (admin mode)", mirroring the exact steps the
///     Infrastructure test fixture uses (<c>tests/Klinkby.Booqr.Infrastructure.Tests/ServiceProviderFixture.cs</c>
///     <c>ProvisionTenant</c>) so tests and production provisioning follow one proven sequence.
/// </summary>
/// <remarks>
///     Everything here connects as <c>booqr_migrator</c> (via the supplied <see cref="NpgsqlDataSource" />)
///     except the seed-admin insert, which opens a short-lived connection as the freshly created
///     <c>t_&lt;id&gt;</c> role so the row's <c>tenant_id</c> DEFAULT (and RLS <c>WITH CHECK</c>)
///     stamp it correctly — <c>booqr_migrator</c> is not a tenant role and is not
///     <c>BYPASSRLS</c>, so it cannot itself insert into <c>app.users</c>.
///     <para>
///         Deliberately takes an already-open <see cref="NpgsqlDataSource" /> plus the raw
///         base-connection-string/master-secret inputs needed to open the tenant connection, rather
///         than building its own — this is what makes it directly testable against the Testcontainers
///         fixture without going through <see cref="AdminRunner" /> or process env vars.
///     </para>
/// </remarks>
[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Role/identifier segments are integer-derived (t_<id>), never a raw slug or " +
                    "other user input; the derived password is a base64url HMAC digest generated " +
                    "here, never attacker input. CREATE/ALTER/DROP ROLE do not accept bind " +
                    "parameters for identifiers or PASSWORD, so string composition is unavoidable; " +
                    "all genuinely variable data (slug, display name, tenant id) is passed as a " +
                    "bind parameter elsewhere in this class.")]
public sealed class TenantProvisioner(
    NpgsqlDataSource migratorDataSource,
    string baseConnectionString,
    string masterSecret)
{
    /// <summary>
    ///     Provisions a new tenant: registry row, <c>t_&lt;id&gt;</c> role, group grant, search_path,
    ///     and a seed Admin user. Refuses if <paramref name="slug" /> fails DNS-label validation or
    ///     if a role named <c>t_&lt;id&gt;</c> already exists (never adopts an orphan).
    /// </summary>
    /// <param name="slug">The tenant's public subdomain label.</param>
    /// <param name="displayName">The tenant's display name (defaults to <paramref name="slug" /> if null/empty).</param>
    /// <param name="seedAdminEmail">Email address for the seeded initial Admin user.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="slug" /> is not a valid DNS label.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the derived role name already exists.</exception>
    public async Task<ProvisionResult> Provision(
        string? slug, string? displayName, string seedAdminEmail, CancellationToken cancellation = default)
    {
        if (!SlugValidator.IsValid(slug))
        {
            throw new ArgumentException(
                "Slug must be a valid DNS label matching ^[a-z0-9]([a-z0-9-]{0,30}[a-z0-9])?$.",
                nameof(slug));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(seedAdminEmail);

        await using NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync(cancellation);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellation);

        int tenantId;
        await using (NpgsqlCommand insert = new(
                         """
                         insert into public.tenants (slug, db_role, display_name)
                         values ($1, 'pending', $2)
                         returning id
                         """, connection, transaction))
        {
            insert.Parameters.Add(new NpgsqlParameter<string> { TypedValue = slug! });
            insert.Parameters.Add(new NpgsqlParameter<string>
            {
                TypedValue = string.IsNullOrWhiteSpace(displayName) ? slug! : displayName
            });
            var result = await insert.ExecuteScalarAsync(cancellation);
            tenantId = (int)result!;
        }

        var role = TenantRole(tenantId);

        // Refuse to adopt an orphaned role: check pg_roles before creating.
        await using (NpgsqlCommand roleCheck = new(
                         "select 1 from pg_roles where rolname = $1", connection, transaction))
        {
            roleCheck.Parameters.Add(new NpgsqlParameter<string> { TypedValue = role });
            var existing = await roleCheck.ExecuteScalarAsync(cancellation);
            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellation);
                throw new InvalidOperationException(
                    $"Refusing to provision tenant '{slug}': role \"{role}\" already exists (orphaned role from a prior failed/incomplete provisioning or deprovisioning).");
            }
        }

        await using (NpgsqlCommand updateRole = new(
                         "update public.tenants set db_role = $1 where id = $2", connection, transaction))
        {
            updateRole.Parameters.Add(new NpgsqlParameter<string> { TypedValue = role });
            updateRole.Parameters.Add(new NpgsqlParameter<int> { TypedValue = tenantId });
            await updateRole.ExecuteNonQueryAsync(cancellation);
        }

        var password = TenantCredentials.DerivePassword(masterSecret, tenantId);

        // Role/identifier is integer-derived (t_<id>) — never a raw slug — so double-quoting it here
        // is safe against identifier injection. The password is a parameter-shaped literal we control
        // (base64url HMAC output, never user input), still passed as a literal because CREATE ROLE
        // does not accept bind parameters for PASSWORD.
        await using (NpgsqlCommand createRole = new(
                         $"""create role "{role}" login password '{EscapeLiteral(password)}' nobypassrls""",
                         connection, transaction))
        {
            await createRole.ExecuteNonQueryAsync(cancellation);
        }

        await using (NpgsqlCommand grant = new($"""grant booqr_tenant to "{role}" """, connection, transaction))
        {
            await grant.ExecuteNonQueryAsync(cancellation);
        }

        await using (NpgsqlCommand searchPath = new(
                         $"""alter role "{role}" set search_path = app""", connection, transaction))
        {
            await searchPath.ExecuteNonQueryAsync(cancellation);
        }

        await transaction.CommitAsync(cancellation);

        // Seed the initial admin user as the tenant role itself, so app.users.tenant_id is stamped
        // by its DEFAULT app.tenant_of(current_user) and satisfies the RLS WITH CHECK — booqr_migrator
        // is neither a tenant role nor BYPASSRLS and cannot insert into app.users directly.
        //
        // This runs on a fresh connection *after* the commit above, so a failure here (e.g. a
        // transient connect failure to the just-created role) would otherwise leave a committed
        // tenant row + role with no admin user — and the pg_roles orphan check would then refuse any
        // re-run. Compensate by deprovisioning the partial tenant so a retry starts clean.
        int seedAdminUserId;
        try
        {
            seedAdminUserId = await SeedAdminUser(tenantId, seedAdminEmail, cancellation);
        }
        catch
        {
            // Best-effort rollback of the partial provision. Swallow cleanup failures so the original
            // seed exception is what surfaces; a leftover row/role would still be recoverable via an
            // explicit --deprovision.
            try
            {
                await Deprovision(tenantId, CancellationToken.None);
            }
            catch (Exception cleanupEx) when (cleanupEx is NpgsqlException or InvalidOperationException)
            {
                // ignored — surface the original seed failure below. A leftover row/role remains
                // recoverable via an explicit --deprovision.
            }

            throw;
        }

        return new ProvisionResult(tenantId, slug!, role, seedAdminUserId, seedAdminEmail);
    }

    /// <summary>
    ///     Soft-deletes the tenant row and drops its <c>t_&lt;id&gt;</c> role. Idempotent with
    ///     respect to the role: if it was already dropped (e.g. a retried/partial prior
    ///     deprovision), that's treated as success, not an error.
    /// </summary>
    public async Task<DeprovisionOutcome> Deprovision(int tenantId, CancellationToken cancellation = default)
    {
        await using NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync(cancellation);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellation);

        int rowsAffected;
        await using (NpgsqlCommand softDelete = new(
                         "update public.tenants set deleted = now(), modified = now() where id = $1 and deleted is null",
                         connection, transaction))
        {
            softDelete.Parameters.Add(new NpgsqlParameter<int> { TypedValue = tenantId });
            rowsAffected = await softDelete.ExecuteNonQueryAsync(cancellation);
        }

        if (rowsAffected == 0)
        {
            await transaction.RollbackAsync(cancellation);
            return DeprovisionOutcome.NotFound;
        }

        await transaction.CommitAsync(cancellation);

        var role = TenantRole(tenantId);
        try
        {
            await using NpgsqlCommand dropRole = new($"""drop role "{role}" """, connection);
            await dropRole.ExecuteNonQueryAsync(cancellation);
            return DeprovisionOutcome.Deprovisioned;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedObject)
        {
            // Role already gone (e.g. a previously interrupted deprovision) — the registry row is
            // authoritative and is already soft-deleted above, so this is not a failure.
            return DeprovisionOutcome.DeprovisionedRoleAlreadyDropped;
        }
    }

    /// <summary>
    ///     Re-derives and applies the password for every non-deleted tenant's <c>t_&lt;id&gt;</c>
    ///     role under <paramref name="newMasterSecret" />. Returns the count rotated.
    /// </summary>
    public async Task<int> Rotate(string newMasterSecret, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newMasterSecret);

        List<int> tenantIds = [];
        await using (NpgsqlConnection readConnection = await migratorDataSource.OpenConnectionAsync(cancellation))
        await using (NpgsqlCommand select = new(
                         "select id from public.tenants where deleted is null order by id", readConnection))
        await using (NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellation))
        {
            while (await reader.ReadAsync(cancellation))
            {
                tenantIds.Add(reader.GetInt32(0));
            }
        }

        var rotated = 0;
        await using NpgsqlConnection writeConnection = await migratorDataSource.OpenConnectionAsync(cancellation);
        foreach (var tenantId in tenantIds)
        {
            var role = TenantRole(tenantId);
            var password = TenantCredentials.DerivePassword(newMasterSecret, tenantId);
            await using NpgsqlCommand alter = new(
                $"""alter role "{role}" password '{EscapeLiteral(password)}'""", writeConnection);
            await alter.ExecuteNonQueryAsync(cancellation);
            rotated++;
        }

        return rotated;
    }

    private async Task<int> SeedAdminUser(int tenantId, string email, CancellationToken cancellation)
    {
        NpgsqlConnectionStringBuilder tenantConnectionStringBuilder = new(baseConnectionString)
        {
            Username = TenantRole(tenantId),
            Password = TenantCredentials.DerivePassword(masterSecret, tenantId)
        };

        await using NpgsqlConnection tenantConnection = new(tenantConnectionStringBuilder.ConnectionString);
        await tenantConnection.OpenAsync(cancellation);

        var now = DateTimeOffset.UtcNow;
        await using NpgsqlCommand insert = new(
            """
            insert into users (email, passwordhash, role, name, phone, created, modified)
            values ($1, null, $2, null, null, $3, $3)
            returning id
            """, tenantConnection);
        insert.Parameters.Add(new NpgsqlParameter<string> { TypedValue = email });
        insert.Parameters.Add(new NpgsqlParameter<string> { TypedValue = Core.UserRole.Admin });
        insert.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { TypedValue = now });

        var result = await insert.ExecuteScalarAsync(cancellation);
        return (int)result!;
    }

    /// <summary>PostgreSQL role name for a tenant id — integer-derived, never a raw slug.</summary>
    internal static string TenantRole(int tenantId) => $"t_{tenantId}";

    /// <summary>
    ///     Escapes a single-quoted SQL string literal. Used only for the derived password (a
    ///     base64url HMAC digest we generate — alphabet is <c>[A-Za-z0-9_-]</c>, so this is
    ///     belt-and-braces, not defense against attacker input) because <c>CREATE ROLE … PASSWORD</c>
    ///     does not accept bind parameters.
    /// </summary>
    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
