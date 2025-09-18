using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Api;

internal sealed partial class Program_LoggerMessages(ILogger logger)
{
    [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
    private readonly ILogger _logger = logger;

    [LoggerMessage(1, LogLevel.Information, "App initialized in {TimeSpan}")]
    public partial void AppLaunch(TimeSpan timeSpan);

    [LoggerMessage(2, LogLevel.Information, "App shutdown ran for {TimeSpan}")]
    public partial void AppShutdown(TimeSpan timeSpan);

    [LoggerMessage(3, LogLevel.Error, "App crash after {TimeSpan}")]
    public partial void AppCrash(Exception exception, TimeSpan timeSpan);
}
