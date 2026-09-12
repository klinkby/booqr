using Microsoft.Extensions.Logging;

namespace Klinkby.Booqr.Api.Worker;

internal sealed partial class WorkerLoggerMessages(ILogger logger)
{
    private readonly ILogger _logger = logger;

    [LoggerMessage(1, LogLevel.Information, "Worker initialized in {TimeSpan}")]
    internal partial void WorkerLaunch(TimeSpan timeSpan);

    [LoggerMessage(2, LogLevel.Information, "Worker shutdown ran for {TimeSpan}")]
    internal partial void WorkerShutdown(TimeSpan timeSpan);

    [LoggerMessage(3, LogLevel.Error, "Worker crash after {TimeSpan}")]
    internal partial void WorkerCrash(Exception exception, TimeSpan timeSpan);
}

internal sealed partial class HeartbeatLoggerMessages(ILogger logger)
{
    private readonly ILogger _logger = logger;

    [LoggerMessage(4, LogLevel.Debug, "Heartbeat service started, writing to {HeartbeatPath}")]
    internal partial void HeartbeatStarted(string heartbeatPath);

    [LoggerMessage(5, LogLevel.Debug, "Heartbeat service stopped")]
    internal partial void HeartbeatStopped();

    [LoggerMessage(6, LogLevel.Trace, "Heartbeat file touched at {HeartbeatPath}")]
    internal partial void HeartbeatTouched(string heartbeatPath);

    [LoggerMessage(7, LogLevel.Warning, "Failed to touch heartbeat file at {HeartbeatPath}")]
    internal partial void HeartbeatTouchFailed(string heartbeatPath, Exception exception);
}
