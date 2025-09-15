using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace Klinkby.Booqr.Infrastructure.Tests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public sealed class ServiceProviderFixture : IAsyncLifetime
{
    private ServiceProvider? _services;

    internal IServiceProvider Services => _services!;

    private PostgreSqlContainer SqlContainer { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:14-alpine")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await SqlContainer.StartAsync();
        _services = new ServiceCollection()
            .AddSingleton<TimeProvider, FakeTimeProvider>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddInfrastructure(options => options.ConnectionString = SqlContainer.GetConnectionString())
            .BuildServiceProvider();
        await InitializeDatabase();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await SqlContainer.DisposeAsync();
        await _services!.DisposeAsync();
    }

    async private Task InitializeDatabase()
    {
        IDatabaseInitializer initializer = Services.GetRequiredService<IDatabaseInitializer>();
        await initializer.Initialize(CancellationToken.None);
    }
}

[CollectionDefinition(nameof(ServiceProviderFixture))]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public class PostgreSqlFixtureCollectionFixture : ICollectionFixture<ServiceProviderFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
