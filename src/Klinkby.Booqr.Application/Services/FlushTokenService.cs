using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Application.Services;

/// <summary>
///     FlushTokenService is a background service that deletes expired and old refresh tokens daily.
/// </summary>
/// <param name="timeProvider">Provides the current time.</param>
/// <param name="serviceProvider">Provides access to registered application services.</param>
/// <param name="logger">Logger instance for logging service activity.</param>
internal sealed partial class FlushTokenService(
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    ILogger<FlushTokenService> logger) : ScheduledBackgroundService(timeProvider, logger)
{
    private static readonly TimeSpan Window = TimeSpan.FromDays(7);
    private readonly LoggerMessages _log = new(logger);
    protected override TimeSpan TriggerTimeOfDay => TimeSpan.Zero; // midnight

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Logging in background task")]
    protected override async Task ExecuteScheduledTaskAsync(CancellationToken cancellation)
    {
        await using AsyncServiceScope serviceScope = serviceProvider.CreateAsyncScope();
        IRefreshTokenRepository repository = serviceScope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        DateTime threshold = Now - Window;
        _log.FlushingTokens(threshold);

        try
        {
            var deletedCount = await repository.Delete(threshold, cancellation);
            _log.FlushComplete(deletedCount);
        }
        catch (Exception ex)
        {
            _log.FlushFailed(ex);
        }
    }

    private sealed partial class LoggerMessages(ILogger<FlushTokenService> logger)
    {
        private readonly ILogger<FlushTokenService> _logger = logger;

        [LoggerMessage(290, LogLevel.Information, "Flushing tokens older than {Threshold}")]
        public partial void FlushingTokens(DateTime threshold);

        [LoggerMessage(291, LogLevel.Information, "Flushed {Count} tokens")]
        public partial void FlushComplete(int count);

        [LoggerMessage(292, LogLevel.Error, "Failed to flush tokens")]
        public partial void FlushFailed(Exception ex);
    }
}
