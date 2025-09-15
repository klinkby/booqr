using Klinkby.Booqr.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Infrastructure.Tests;

[Collection(nameof(ServiceProviderFixture))]
public sealed class BookingRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IServiceRepository _services = serviceProvider.Services.GetRequiredService<IServiceRepository>();
    private readonly IBookingRepository _sut = serviceProvider.Services.GetRequiredService<IBookingRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();
    private readonly IUserRepository _users = serviceProvider.Services.GetRequiredService<IUserRepository>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_Booking_WHEN_Add_THEN_CanBeReadBack(Service service, User customer, Booking expected)
    {
        int newId;
        Booking? actual;
        await _transaction.Begin();
        try
        {
            expected = expected with
            {
                ServiceId = await _services.Add(service),
                CustomerId = await _users.Add(customer with { Role = UserRole.Customer })
            };
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
