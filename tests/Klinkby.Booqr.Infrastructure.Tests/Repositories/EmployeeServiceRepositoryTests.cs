namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public sealed class EmployeeServiceRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IEmployeeServiceRepository _sut = serviceProvider.Services.GetRequiredService<IEmployeeServiceRepository>();
    private readonly IServiceRepository _services = serviceProvider.Services.GetRequiredService<IServiceRepository>();
    private readonly IUserRepository _users = serviceProvider.Services.GetRequiredService<IUserRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_EmployeeAndService_WHEN_Add_THEN_CanBeReadBack(User employee, Service service)
    {
        int employeeId;
        int serviceId;
        Service[] actual;
        await _transaction.Begin();
        try
        {
            employeeId = await _users.Add(employee with { Role = UserRole.Employee });
            serviceId = await _services.Add(service);

            await _sut.Add(employeeId, serviceId);
            actual = await _sut.GetByEmployeeId(employeeId, new PageQuery(), TestContext.Current.CancellationToken)
                .ToArrayAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Single(actual);
        Assert.Equal(serviceId, actual[0].Id);
        Assert.Equal(service.Name, actual[0].Name);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_Existing_WHEN_Delete_THEN_RemovedFromResults(User employee, Service service)
    {
        int employeeId;
        bool deleted;
        Service[] actual;
        await _transaction.Begin();
        try
        {
            employeeId = await _users.Add(employee with { Role = UserRole.Employee });
            int serviceId = await _services.Add(service);

            await _sut.Add(employeeId, serviceId);
            deleted = await _sut.Delete(employeeId, serviceId);
            actual = await _sut.GetByEmployeeId(employeeId, new PageQuery(), TestContext.Current.CancellationToken)
                .ToArrayAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.True(deleted);
        Assert.Empty(actual);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_NonExistent_WHEN_Delete_THEN_ReturnsFalse(User employee)
    {
        bool deleted;
        await _transaction.Begin();
        try
        {
            int employeeId = await _users.Add(employee with { Role = UserRole.Employee });
            deleted = await _sut.Delete(employeeId, int.MaxValue);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.False(deleted);
    }
}
