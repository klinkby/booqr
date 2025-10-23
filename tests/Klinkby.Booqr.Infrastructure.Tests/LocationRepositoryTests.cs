namespace Klinkby.Booqr.Infrastructure.Tests;

[Collection(nameof(ServiceProviderFixture))]
public sealed class LocationRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly ILocationRepository _sut = serviceProvider.Services.GetRequiredService<ILocationRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_Location_WHEN_Add_THEN_CanBeReadBack(Location expected)
    {
        int newId;
        Location? actual;
        await _transaction.Begin();
        try
        {
            newId = await _sut.Add(expected);
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
