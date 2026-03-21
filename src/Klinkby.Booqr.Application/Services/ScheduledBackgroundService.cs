using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Klinkby.Booqr.Application.Services;

/// <summary>
///     Abstract base class for services that run on a daily schedule.
///     Before executing the scheduled task each instance attempts to claim the execution slot via
///     <see cref="IJobClaim"/>, ensuring only one instance runs the task when scaled out.
/// </summary>
internal abstract partial class ScheduledBackgroundService(
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    ILogger logger) : BackgroundService
{
    private readonly LoggerMessages _log = new(logger);

    protected DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
    protected abstract TimeSpan TriggerTimeOfDay { get; }
    protected abstract string JobName { get; }
    protected IServiceProvider ServiceProvider => serviceProvider;

    internal DateTime GetNext(DateTime now)
    {
        return now.Date.AddDays(now.TimeOfDay >= TriggerTimeOfDay ? 1 : 0) + TriggerTimeOfDay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime now = Now;
            DateTime next = GetNext(now);
            TimeSpan timeToNext = next - now;

            _log.Sleep(timeToNext, next);

            try
            {
                await Task.Delay(timeToNext, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ExecuteIfClaimedAsync(stoppingToken);
        }
    }

    private async Task ExecuteIfClaimedAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IJobClaim jobClaim = scope.ServiceProvider.GetRequiredService<IJobClaim>();

        if (await jobClaim.TryClaimAsync(JobName, DateOnly.FromDateTime(Now), stoppingToken))
        {
            await ExecuteScheduledTaskAsync(stoppingToken);
        }
        else
        {
            _log.Skipped(JobName);
        }
    }

    protected abstract Task ExecuteScheduledTaskAsync(CancellationToken cancellation);

    private sealed partial class LoggerMessages(ILogger logger)
    {
        private readonly ILogger _logger = logger;

        [LoggerMessage(290, LogLevel.Information, "Sleep for {Duration} until {Next}")]
        public partial void Sleep(TimeSpan duration, DateTime next);

        [LoggerMessage(291, LogLevel.Information, "Skipping {JobName}: already claimed by another instance")]
        public partial void Skipped(string jobName);
    }
}
