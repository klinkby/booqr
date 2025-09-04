using System.Diagnostics.CodeAnalysis;
using AutoFixture;
using Klinkby.Booqr.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace Klinkby.Booqr.Infrastructure.Tests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public sealed class ServiceProviderFixture : IAsyncLifetime
{
    private readonly Fixture _fixture = new();
    private ServiceProvider? _services;
    private TestData? _testData;

    internal IServiceProvider Services => _services!;
    internal TestData TestData => _testData!;

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
        await InitializeUserTestData();
    }

    async private Task InitializeUserTestData()
    {
        IUserRepository users = Services.GetRequiredService<IUserRepository>();
        ILocationRepository locations = Services.GetRequiredService<ILocationRepository>();
        _fixture.Customize<User>(c => c.Without(p => p.Deleted));
        _fixture.Customize<Location>(c => c
            .With(p => p.Zip, () => "2301")
            .Without(p => p.Deleted));
        _testData = new TestData(
            await users.Add(_fixture.Create<User>() with { Role = UserRole.Employee }),
            await users.Add(_fixture.Create<User>() with { Role = UserRole.Employee }),
            await locations.Add(_fixture.Create<Location>()),
            await users.Add(_fixture.Create<User>() with { Role = UserRole.Customer }));
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
