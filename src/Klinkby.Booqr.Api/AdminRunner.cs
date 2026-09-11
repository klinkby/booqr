namespace Klinkby.Booqr.Api;

/// <summary>
///     Dispatch scaffold for admin mode (docs/1-design.md "2b. Admin CLI (admin mode)"), selected by
///     a leading <c>admin</c> argument (<c>Program.cs</c>): <c>admin --migrate</c>,
///     <c>admin --provision &lt;slug&gt;</c>, <c>admin --deprovision &lt;id&gt;</c>,
///     <c>admin --rotate</c>.
/// </summary>
/// <remarks>
///     Scope: this type only detects which admin command was requested and dispatches to the
///     matching seam method below. The actual implementations (provisioning a tenant role,
///     applying migrations via <c>SchemaMigrator</c>, deprovisioning, rotating tenant passwords) are
///     Phase 5 and intentionally throw <see cref="NotImplementedException" /> here. Building a
///     minimal host/service provider for the admin command (so it can reach
///     <c>booqr_migrator</c>/<c>booqr_batch</c> and the registry) is also Phase 5 work — this
///     scaffold takes no dependency on Infrastructure DI so the web-host startup path
///     (<c>ConfigureServices</c>/<c>ConfigureMiddleware</c>) is completely untouched when the
///     <c>admin</c> flag is absent.
/// </remarks>
internal static class AdminRunner
{
    private const int UsageErrorExitCode = 64; // EX_USAGE (sysexits.h convention)

    /// <summary>
    ///     Parses the admin subcommand and its arguments, dispatches to the matching Phase-5 seam,
    ///     and returns the process exit code. Never starts Kestrel or any web pipeline.
    /// </summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            await Console.Error.WriteLineAsync(
                "Usage: admin --migrate | --provision <slug> | --deprovision <id> | --rotate");
            return UsageErrorExitCode;
        }

        return args[0] switch
        {
            "--migrate" => await Migrate(),
            "--provision" => await Provision(args.ElementAtOrDefault(1)),
            "--deprovision" => await Deprovision(args.ElementAtOrDefault(1)),
            "--rotate" => await Rotate(),
            _ => await UnknownCommand(args[0])
        };
    }

    /// <summary>Phase 5 seam: apply pending migrations via <c>SchemaMigrator</c>, connected as <c>booqr_migrator</c>.</summary>
    private static Task<int> Migrate() =>
        throw new NotImplementedException(
            "admin --migrate is implemented in Phase 5 (docs/2-implementation.md, Phase 5).");

    /// <summary>Phase 5 seam: provision a new tenant (registry row, role, seed admin user, activation link).</summary>
    private static Task<int> Provision(string? slug) =>
        throw new NotImplementedException(
            "admin --provision is implemented in Phase 5 (docs/2-implementation.md, Phase 5).");

    /// <summary>Phase 5 seam: soft-delete a tenant and drop/lock its role.</summary>
    private static Task<int> Deprovision(string? tenantId) =>
        throw new NotImplementedException(
            "admin --deprovision is implemented in Phase 5 (docs/2-implementation.md, Phase 5).");

    /// <summary>Phase 5 seam: rotate every tenant role's derived password under a new master secret.</summary>
    private static Task<int> Rotate() =>
        throw new NotImplementedException(
            "admin --rotate is implemented in Phase 5 (docs/2-implementation.md, Phase 5).");

    private static async Task<int> UnknownCommand(string command)
    {
        await Console.Error.WriteLineAsync($"Unknown admin command '{command}'.");
        return UsageErrorExitCode;
    }
}
