using Microsoft.Extensions.Hosting;

namespace Klinkby.Booqr.Application.Services;

/// <summary>
///     Abstract base class for services that run on a daily schedule.
/// </summary>
internal abstract partial class ScheduledBackgroundService(
    TimeProvider timeProvider,
    ILogger logger) : BackgroundService
{
    private readonly LoggerMessages _log = new(logger);

    protected DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
    protected abstract TimeSpan TriggerTimeOfDay { get; }

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
                await Task.Delay(timeToNext, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ExecuteScheduledTaskAsync(stoppingToken);
        }
    }

    protected abstract Task ExecuteScheduledTaskAsync(CancellationToken cancellation);

    private sealed partial class LoggerMessages(ILogger logger)
    {
        private readonly ILogger _logger = logger;

        [LoggerMessage(290, LogLevel.Information, "Sleep for {Duration} until {Next}")]
        public partial void Sleep(TimeSpan duration, DateTime next);
    }
}
