using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Dapper;
using Klinkby.Booqr.Infrastructure.Models;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

[module: DapperAot]

namespace Klinkby.Booqr.Infrastructure.Tests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public sealed class ServiceProviderFixture : IAsyncLifetime
{
    private ServiceProvider? _services;

    internal IServiceProvider Services => _services!;

    private PostgreSqlContainer SqlContainer { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine3.22")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await SqlContainer.StartAsync();
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { nameof(InfrastructureSettings.ConnectionString), SqlContainer.GetConnectionString() }
            })
            .Build();
        _services = new ServiceCollection()
            .AddSingleton<TimeProvider, FakeTimeProvider>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddInfrastructure(config)
            .BuildServiceProvider();
        await InitializeDatabase();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await SqlContainer.DisposeAsync();
        await _services!.DisposeAsync();
    }

    async private Task InitializeDatabase(CancellationToken cancellationToken = default)
    {
        IConnectionProvider connectionProvider = Services.GetRequiredService<IConnectionProvider>();
        DbConnection connection = await connectionProvider.GetConnection(cancellationToken);
        using StreamReader sr = new(
            typeof(ServiceProviderFixture)
                .Assembly
                .GetManifestResourceStream("Klinkby.Booqr.Infrastructure.Tests.initdb.sql")!);
        var initScript = await sr.ReadToEndAsync(cancellationToken);
        await connection.ExecuteScalarAsync(initScript);
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
