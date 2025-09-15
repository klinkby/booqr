using Klinkby.Booqr.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Infrastructure.Tests;

[Collection(nameof(ServiceProviderFixture))]
public sealed class UserRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IUserRepository _sut = serviceProvider.Services.GetRequiredService<IUserRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_User_WHEN_Add_THEN_CanBeReadBack(User expected)
    {
        int newId;
        User? actual1;
        User? actual2;
        await _transaction.Begin();
        try
        {
            newId = await _sut.Add(expected);
            actual1 = await _sut.GetById(newId);
            actual2 = await _sut.GetByEmail(expected.Email);
            await _sut.Delete(newId);
            await _sut.Undelete(newId);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.InRange(newId, 1, int.MaxValue);
        Assert.Equal(expected with
            {
                Id = actual1!.Id,
                Created = actual1.Created,
                Modified = actual1.Modified,
                Version = actual1.Version
            },
            actual1);
        Assert.Equal(actual1.Id, actual2!.Id);
    }
}
