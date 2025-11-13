using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Api;

internal sealed partial class ProgramLoggerMessages(ILogger logger)
{
    [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Referenced by source generator")]
    private readonly ILogger _logger = logger;

    [LoggerMessage(1, LogLevel.Information, "App initialized in {TimeSpan}")]
    internal partial void AppLaunch(TimeSpan timeSpan);

    [LoggerMessage(2, LogLevel.Information, "App shutdown ran for {TimeSpan}")]
    internal partial void AppShutdown(TimeSpan timeSpan);

    [LoggerMessage(3, LogLevel.Error, "App crash after {TimeSpan}")]
    internal partial void AppCrash(Exception exception, TimeSpan timeSpan);
}
