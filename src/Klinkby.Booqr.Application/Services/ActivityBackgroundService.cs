using System.Data.Common;
using System.Threading.Channels;
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

    private sealed partial class LoggerMessages(ILogger<ActivityBackgroundService> logger)
    {
        private readonly ILogger<ActivityBackgroundService> _logger = logger;

        [LoggerMessage(1030, LogLevel.Warning, "Error adding activity: {Message}")]
        public partial void AddActivityFailed(Exception ex, string message);
    }
}
