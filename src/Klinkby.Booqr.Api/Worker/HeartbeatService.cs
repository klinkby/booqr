using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Api.Worker;

/// <summary>
///     Periodically touches a heartbeat file to signal liveness to the container health-check.
///     The health-check asserts the file was modified recently (e.g., <c>find <path> -mmin -2</c>).
/// </summary>
/// <remarks>
///     Runs as a dependency-free IHostedService with no database or external dependencies.
///     Registers its own logger via <see cref="HeartbeatLoggerMessages" />.
///     Timer is driven by <see cref="TimeProvider.CreateTimer" /> (not PeriodicTimer) so
///     it respects <c>FakeTimeProvider</c> in tests.
/// </remarks>
internal sealed partial class HeartbeatService(
    TimeProvider timeProvider,
    ILogger<HeartbeatService> logger,
    string heartbeatPath)
    : IHostedService, IDisposable
{
    private readonly LoggerMessages _log = new(logger);
    private ITimer? _timer;

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Touch the file immediately so it exists before the first timer tick.
        Touch();
        _log.HeartbeatStarted(heartbeatPath);

        // Start a timer that fires every 30 seconds (not PeriodicTimer, since tests use FakeTimeProvider).
        _timer = timeProvider.CreateTimer(
            _ => Touch(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _log.HeartbeatStopped();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Ensures the heartbeat file exists and updates its last-write timestamp to the current time
    ///     (from <c>timeProvider.GetUtcNow()</c>, so tests can advance time deterministically).
    /// </summary>
    private void Touch()
    {
        try
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            // Ensure parent directory exists.
            var directory = Path.GetDirectoryName(heartbeatPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write a minimal payload if file doesn't exist yet.
            if (!File.Exists(heartbeatPath))
            {
                File.WriteAllText(heartbeatPath, "alive");
            }

            // Update the last-write time from the TimeProvider (crucial for test determinism).
            File.SetLastWriteTimeUtc(heartbeatPath, now);
            _log.HeartbeatTouched(heartbeatPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a failed heartbeat write must never crash the worker. The
            // container health-check will observe the stale file and restart if it persists.
            _log.HeartbeatTouchFailed(heartbeatPath, ex);
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Referenced by source generator")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(1073, LogLevel.Debug, "Heartbeat service started, writing to {HeartbeatPath}")]
        internal partial void HeartbeatStarted(string heartbeatPath);

        [LoggerMessage(1074, LogLevel.Debug, "Heartbeat service stopped")]
        internal partial void HeartbeatStopped();

        [LoggerMessage(1075, LogLevel.Trace, "Heartbeat file touched at {HeartbeatPath}")]
        internal partial void HeartbeatTouched(string heartbeatPath);

        [LoggerMessage(1076, LogLevel.Warning, "Failed to touch heartbeat file at {HeartbeatPath}")]
        internal partial void HeartbeatTouchFailed(string heartbeatPath, Exception exception);
    }
}
