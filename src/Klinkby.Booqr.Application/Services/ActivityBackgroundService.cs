using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Klinkby.Booqr.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Klinkby.Booqr.Application.Services;

/// <summary>
/// A background service that add activities asynchronously. The service reads activities from a channel
/// and commit them to DB using a scoped service provider.
/// </summary>
internal sealed partial class ActivityBackgroundService(
    ChannelReader<Activity> reader,
    IServiceProvider serviceProvider,
    ILogger<ActivityBackgroundService> logger) : BackgroundService
{
    private readonly LoggerMessages _log = new(logger);

    async protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Activity activity in reader.ReadAllAsync(stoppingToken))
        {
            using IDisposable? loggerScope = logger.BeginScope(new { });
            await using AsyncServiceScope serviceScope = serviceProvider.CreateAsyncScope();
            await TryAddActivity(serviceScope.ServiceProvider, activity, stoppingToken);
        }
    }

    async private Task TryAddActivity(IServiceProvider scopedServiceProvider, Activity activity, CancellationToken stoppingToken)
    {
        // Cross-tenant consumer: writes Activity.TenantId explicitly via the booqr_batch
        // (BYPASSRLS) connection, since booqr_batch's app.tenant_of(current_user) resolves to no
        // tenant (see docs/1-design.md "Background / scheduled work"). Enable before resolving the
        // repository, so ActivityRepository observes IBatchScope.IsEnabled for this scope.
        scopedServiceProvider.GetRequiredService<IBatchScope>().Enable();
        IActivityRepository activities = scopedServiceProvider.GetRequiredService<IActivityRepository>();
        try
        {
            _ = await activities.Add(activity, stoppingToken);
        }
        catch (DbException ex)
        {
            _log.AddActivityFailed(ex, ex.Message);
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger<ActivityBackgroundService> logger)
    {
        private readonly ILogger<ActivityBackgroundService> _logger = logger;

        [LoggerMessage(250, LogLevel.Warning, "Error adding activity: {Message}")]
        public partial void AddActivityFailed(Exception ex, string message);
    }
}
