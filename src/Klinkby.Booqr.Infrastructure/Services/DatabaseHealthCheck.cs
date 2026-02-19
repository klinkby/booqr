using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Klinkby.Booqr.Infrastructure.Services;

[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
internal sealed partial class DatabaseHealthCheck(IConnectionProvider connectionProvider, ILogger<DatabaseHealthCheck> logger)
    : IHealthCheck
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connectionProvider.GetConnection(cancellationToken);
            return connection.State == ConnectionState.Open
                ? HealthCheckResult.Healthy("Database connection is available.")
                : HealthCheckResult.Unhealthy("Database connection is not open.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.HealthCheckFailed(ex);
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }

    private sealed partial class LoggerMessages(ILogger<DatabaseHealthCheck> logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger<DatabaseHealthCheck> _logger = logger;

        [LoggerMessage(1042, LogLevel.Error, "Database health check failed")]
        internal partial void HealthCheckFailed(Exception exception);
    }
}
