using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public sealed class TenantNameRepositoryTests(ServiceProviderFixture serviceProvider)
{
    [Fact]
    public async Task GIVEN_BatchScope_WHEN_GetDisplayNames_THEN_ReturnsAllLiveTenants()
    {
        await using AsyncServiceScope scope = serviceProvider.WorkerServices.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IBatchScope>().Enable();
        ITenantNameRepository sut = scope.ServiceProvider.GetRequiredService<ITenantNameRepository>();

        IReadOnlyDictionary<int, string> names = await sut.GetDisplayNames(CancellationToken.None);

        Assert.Equal("Display tenant-a", names[serviceProvider.TenantAId]);
        Assert.Equal("Display tenant-b", names[serviceProvider.TenantBId]);
    }

    [Fact]
    public async Task GIVEN_TenantConnection_WHEN_GetDisplayNames_THEN_PermissionDenied()
    {
        await using AsyncServiceScope scope = serviceProvider.Services.CreateAsyncScope();
        ITenantNameRepository sut = scope.ServiceProvider.GetRequiredService<ITenantNameRepository>();

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => sut.GetDisplayNames(CancellationToken.None));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }
}
