using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Klinkby.Booqr.Infrastructure.Tests.Services;

[Collection(nameof(ServiceProviderFixture))]
public class DatabaseHealthCheckTests(ServiceProviderFixture fixture)
{
    [Fact]
    public async Task GIVEN_ValidDatabase_WHEN_CheckHealthAsync_THEN_ReturnsHealthy()
    {
        // Arrange
        var connectionProvider = fixture.Services.GetRequiredService<IConnectionProvider>();
        var healthCheck = new DatabaseHealthCheck(connectionProvider);
        var context = new HealthCheckContext();

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Database connection is available", result.Description);
    }
}
