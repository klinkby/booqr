using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Klinkby.Booqr.Api.Worker;

/// <summary>
/// Periodically touches a heartbeat file to signal liveness to the container health-check.
/// The health-check asserts the file was modified recently (e.g., <c>find <path> -mmin -2</c>).
/// </summary>
/// <remarks>
/// Runs as a dependency-free IHostedService with no database or external dependencies.
/// Registers its own logger via <see cref="HeartbeatLoggerMessages"/>.
/// Timer is driven by <see cref="TimeProvider.CreateTimer"/> (not PeriodicTimer) so
/// it respects <c>FakeTimeProvider</c> in tests.
/// </remarks>
internal sealed class HeartbeatService(TimeProvider timeProvider, ILogger<HeartbeatService> logger, string heartbeatPath)
    : IHostedService, IDisposable
{
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly string _heartbeatPath = heartbeatPath;
    private ITimer? _timer;
    private readonly HeartbeatLoggerMessages _log = new(logger);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Touch the file immediately so it exists before the first timer tick.
        Touch();
        _log.HeartbeatStarted(_heartbeatPath);

        // Start a timer that fires every 30 seconds (not PeriodicTimer, since tests use FakeTimeProvider).
        _timer = _timeProvider.CreateTimer(
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

    public void Dispose()
    {
        _timer?.Dispose();
    }

    /// <summary>
    /// Ensures the heartbeat file exists and updates its last-write timestamp to the current time
    /// (from <c>timeProvider.GetUtcNow()</c>, so tests can advance time deterministically).
    /// </summary>
    private void Touch()
    {
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Ensure parent directory exists.
            var directory = Path.GetDirectoryName(_heartbeatPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write a minimal payload if file doesn't exist yet.
            if (!File.Exists(_heartbeatPath))
            {
                File.WriteAllText(_heartbeatPath, "alive");
            }

            // Update the last-write time from the TimeProvider (crucial for test determinism).
            File.SetLastWriteTimeUtc(_heartbeatPath, now);
            _log.HeartbeatTouched(_heartbeatPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a failed heartbeat write must never crash the worker. The
            // container health-check will observe the stale file and restart if it persists.
            _log.HeartbeatTouchFailed(_heartbeatPath, ex);
        }
    }
}
