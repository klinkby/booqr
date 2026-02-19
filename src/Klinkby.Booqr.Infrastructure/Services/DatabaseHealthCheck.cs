using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Klinkby.Booqr.Infrastructure.Services;

internal sealed class DatabaseHealthCheck(IConnectionProvider connectionProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DbConnection connection = await connectionProvider.GetConnection(cancellationToken);
            
            // Simple check: verify connection is open and can execute a basic query
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            
            return result is not null
                ? HealthCheckResult.Healthy("Database connection is available")
                : HealthCheckResult.Unhealthy("Database query returned null");
        }
#pragma warning disable CA1031 // Health checks should catch all exceptions
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
#pragma warning restore CA1031
    }
}
