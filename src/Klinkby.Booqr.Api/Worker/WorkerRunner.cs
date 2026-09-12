using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using NLog.Extensions.Logging;

namespace Klinkby.Booqr.Api.Worker;

/// <summary>
///     Composition root for worker mode (the "worker" leading argument, mirroring the admin-mode
///     dispatch in <c>Program.cs</c>): the same Native-AOT image, started as a generic host with no
///     Kestrel, no web pipeline, and no JWT authentication.
/// </summary>
/// <remarks>
///     <para>
///         <b>Security boundary — this is the whole point of the worker run-mode.</b> The worker
///         runs the two cross-tenant scheduled jobs (reminder mail, refresh-token flush) as the
///         fixed <c>booqr_batch</c> (<c>BYPASSRLS</c>) role via <c>AddWorkerInfrastructure</c>
///         (<c>Klinkby.Booqr.Infrastructure.ServiceCollectionExtensions</c>). It deliberately never
///         registers:
///     </para>
///     <list type="bullet">
///         <item><description>
///         JWT (<c>JwtSettings</c>/<c>Jwt</c> config section, <c>IOAuth</c>) — the worker issues
///         and validates no tokens; it is not on any authenticated request path.
///         </description></item>
///         <item><description>
///         Tenant data sources / tenant registry (<c>AddTenantDataSources</c>,
///         <c>AddTenantRegistry</c>) — these load the tenant master secret
///         (<c>TenantDataSources:MasterSecret</c>) used to derive every tenant's per-tenant login
///         role password. The worker has no business minting or resolving a tenant connection, so
///         it must never hold that secret in memory: smaller blast radius if this process is
///         ever compromised.
///         </description></item>
///         <item><description>
///         Kestrel / the web pipeline / <c>PasswordSettings</c> — no HTTP surface at all.
///         </description></item>
///     </list>
///     <para>
///         It resolves only what the scheduled services in
///         <c>Klinkby.Booqr.Application.Workers</c> actually inject: repositories (via
///         <c>AddRepositories</c>), <see cref="IMailClient"/>, <c>IBatchScope</c>/the keyed batch
///         <c>DbConnection</c>, the <c>ReminderMailSettings</c> option, and the two
///         <see cref="IHostedService"/> registrations themselves
///         (<c>AddScheduledWorkers</c> in <c>Klinkby.Booqr.Application</c>).
///     </para>
/// </remarks>
public static class WorkerRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var timer = Stopwatch.StartNew();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();

        builder.Services.AddSingleton<TimeProvider>(static _ => TimeProvider.System);

        // Only the two scheduled services' own dependencies - never AddApplication (which requires
        // and throws on missing Jwt/Password sections) and never AddInfrastructure (which loads the
        // tenant master secret). See the security-boundary remarks above.
        builder.Services.AddScheduledWorkers(builder.Configuration.GetRequiredSection("Application"));
        builder.Services.AddWorkerInfrastructure(builder.Configuration.GetRequiredSection("Infrastructure"));

        IHost host = builder.Build();
        WorkerLoggerMessages log = new(host.Services.GetRequiredService<ILogger<Program>>());

        try
        {
            log.WorkerLaunch(timer.Elapsed);
            await host.RunAsync();
            log.WorkerShutdown(timer.Elapsed);
            return 0;
        }
        catch (Exception exception)
        {
            log.WorkerCrash(exception, timer.Elapsed);
            throw;
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }
}
