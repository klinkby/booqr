using Klinkby.Booqr.Infrastructure.Services;
using Npgsql;

namespace Klinkby.Booqr.Api.Admin;

/// <summary>
///     Dispatch for admin mode (docs/1-design.md "2b. Admin CLI (admin mode)"), selected by a
///     leading <c>admin</c> argument (the API's <c>Program.cs</c>): <c>admin --migrate</c>,
///     <c>admin --provision &lt;slug&gt;</c>, <c>admin --deprovision &lt;id&gt;</c>,
///     <c>admin --rotate</c>.
/// </summary>
/// <remarks>
///     Lives in <c>Klinkby.Booqr.Control</c> — the control-plane assembly that holds the elevated
///     <c>booqr_migrator</c>/<c>booqr_batch</c> provisioning logic. Keeping it out of the
///     request-path assemblies (Api/Application) makes "the migrator/batch credentials never run on
///     a request path" a compile-time boundary (enforced by <c>ControlTests</c>), not just a
///     convention.
///     <para>
///     Each command builds its own minimal <see cref="NpgsqlDataSource" /> from
///     <see cref="TenantProvisioner" /> (environment variables) — never a web host, never DI. The
///     provisioning/deprovision/rotation logic itself lives in <see cref="AdminConfig" /> so it
///     can be exercised directly by integration tests without going through this process-args/env
///     seam.
///     </para>
/// </remarks>
public static class AdminRunner
{
    private const int UsageErrorExitCode = 64; // EX_USAGE (sysexits.h convention)
    private const int FailureExitCode = 1;

    /// <summary>
    ///     Parses the admin subcommand and its arguments, dispatches to the matching command, and
    ///     returns the process exit code. Never starts Kestrel or any web pipeline.
    /// </summary>
    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            await Console.Error.WriteLineAsync(
                "Usage: admin --migrate | --provision <slug> [seedAdminEmail] | --deprovision <id> | --rotate");
            return UsageErrorExitCode;
        }

        return args[0] switch
        {
            "--migrate" => await Migrate(),
            "--provision" => await Provision(args.ElementAtOrDefault(1), args.ElementAtOrDefault(2)),
            "--deprovision" => await Deprovision(args.ElementAtOrDefault(1)),
            "--rotate" => await Rotate(),
            _ => await UnknownCommand(args[0])
        };
    }

    /// <summary>Applies pending migrations via <see cref="SchemaMigrator" />, connected as <c>booqr_migrator</c>.</summary>
    private static async Task<int> Migrate()
    {
        try
        {
            await using NpgsqlDataSource migratorDataSource =
                NpgsqlDataSource.Create(AdminConfig.MigratorConnectionString());
            SchemaMigrator migrator = new(migratorDataSource);
            await migrator.Migrate();

            await Console.Out.WriteLineAsync("Migrations applied. RLS coverage guard passed.");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NpgsqlException)
        {
            await Console.Error.WriteLineAsync($"Migration failed: {ex.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>
    ///     Provisions a new tenant (registry row, role, seed admin user), then emits the seeded
    ///     admin's id/email to stdout. See <see cref="TenantProvisioner.Provision" /> for the
    ///     activation-link design note.
    /// </summary>
    private static async Task<int> Provision(string? slug, string? seedAdminEmail)
    {
        if (!SlugValidator.IsValid(slug))
        {
            await Console.Error.WriteLineAsync(
                $"Invalid slug '{slug}'. A slug must be a DNS label matching ^[a-z0-9]([a-z0-9-]{{0,30}}[a-z0-9])?$.");
            return UsageErrorExitCode;
        }

        seedAdminEmail ??= $"admin@{slug}.booqr.dk";

        try
        {
            var baseConnectionString = AdminConfig.BaseConnectionString();
            var masterSecret = AdminConfig.MasterSecret();

            await using NpgsqlDataSource migratorDataSource =
                NpgsqlDataSource.Create(AdminConfig.MigratorConnectionString());
            TenantProvisioner provisioner = new(migratorDataSource, baseConnectionString, masterSecret);

            ProvisionResult result = await provisioner.Provision(slug, slug, seedAdminEmail);

            await Console.Out.WriteLineAsync($"Provisioned tenant {result.TenantId} (slug '{result.Slug}', role \"{result.DbRole}\").");
            await Console.Out.WriteLineAsync($"Seeded admin user id {result.SeedAdminUserId} <{result.SeedAdminEmail}>.");
            await Console.Out.WriteLineAsync(
                "No activation link was emitted: signed-link generation (ExpiringQueryString/PasswordSettings.HmacKey) "
                + "lives in Klinkby.Booqr.Application, which Control must not reference (see docs/2-implementation.md "
                + "layer boundaries; escalated to the orchestrator per the Phase-5 spec). Trigger activation for this "
                + "user via the app's existing password-reset flow (POST /api/users/reset-password or equivalent) "
                + "against the tenant's own subdomain.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            await Console.Error.WriteLineAsync($"Invalid input: {ex.Message}");
            return UsageErrorExitCode;
        }
        catch (InvalidOperationException ex)
        {
            // Includes "role already exists" refusal.
            await Console.Error.WriteLineAsync($"Provisioning failed: {ex.Message}");
            return FailureExitCode;
        }
        catch (PostgresException ex)
        {
            await Console.Error.WriteLineAsync($"Provisioning failed: {ex.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>Soft-deletes a tenant and drops its role.</summary>
    private static async Task<int> Deprovision(string? tenantIdArg)
    {
        if (!int.TryParse(tenantIdArg, out var tenantId))
        {
            await Console.Error.WriteLineAsync($"Invalid tenant id '{tenantIdArg}'. Must be an integer.");
            return UsageErrorExitCode;
        }

        try
        {
            await using NpgsqlDataSource migratorDataSource =
                NpgsqlDataSource.Create(AdminConfig.MigratorConnectionString());
            TenantProvisioner provisioner = new(
                migratorDataSource, AdminConfig.BaseConnectionString(), AdminConfig.MasterSecret());

            DeprovisionOutcome outcome = await provisioner.Deprovision(tenantId);

            switch (outcome)
            {
                case DeprovisionOutcome.NotFound:
                    await Console.Error.WriteLineAsync($"No active tenant with id {tenantId} found.");
                    return FailureExitCode;
                case DeprovisionOutcome.DeprovisionedRoleAlreadyDropped:
                    await Console.Out.WriteLineAsync(
                        $"Tenant {tenantId} soft-deleted. Role \"t_{tenantId}\" was already dropped.");
                    return 0;
                // case DeprovisionOutcome.Deprovisioned:
                default:
                    await Console.Out.WriteLineAsync($"Tenant {tenantId} soft-deleted and role \"t_{tenantId}\" dropped.");
                    return 0;
            }
        }
        catch (PostgresException ex)
        {
            await Console.Error.WriteLineAsync($"Deprovisioning failed: {ex.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>Rotates every tenant role's derived password under a new master secret.</summary>
    private static async Task<int> Rotate()
    {
        try
        {
            var newMasterSecret = AdminConfig.NewMasterSecret();

            await using NpgsqlDataSource migratorDataSource =
                NpgsqlDataSource.Create(AdminConfig.MigratorConnectionString());
            TenantProvisioner provisioner = new(
                migratorDataSource, AdminConfig.BaseConnectionString(), AdminConfig.MasterSecret());

            var rotated = await provisioner.Rotate(newMasterSecret);

            await Console.Out.WriteLineAsync($"Rotated {rotated} tenant role password(s).");
            return 0;
        }
        catch (PostgresException ex)
        {
            await Console.Error.WriteLineAsync($"Rotation failed: {ex.Message}");
            return FailureExitCode;
        }
    }

    private static async Task<int> UnknownCommand(string command)
    {
        await Console.Error.WriteLineAsync($"Unknown admin command '{command}'.");
        return UsageErrorExitCode;
    }
}
