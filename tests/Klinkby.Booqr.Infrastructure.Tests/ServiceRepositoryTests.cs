namespace Klinkby.Booqr.Infrastructure.Tests;

[Collection(nameof(ServiceProviderFixture))]
public sealed class ServiceRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IServiceRepository _sut = serviceProvider.Services.GetRequiredService<IServiceRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_Service_WHEN_Add_THEN_CanBeReadBack(Service expected)
    {
        int newId;
        Service? actual;
        await _transaction.Begin();
        try
        {
            newId = await _sut.Add(expected);
            await _sut.Update(expected);
            await _sut.Delete(newId);
            await _sut.Undelete(newId);
            actual = await _sut.GetById(newId);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.InRange(newId, 1, int.MaxValue);
        Assert.Equal(expected with
            {
                Id = actual!.Id,
                Created = actual.Created,
                Modified = actual.Modified,
                Version = actual.Version
            },
            actual);
    }
}
