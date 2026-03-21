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
    public async Task GIVEN_NoAssignment_WHEN_Assign_THEN_EmployeesAddedToService(Service service, User employee)
    {
        int serviceId;
        int employeeId;
        Service? actual;
        await _transaction.Begin();
        try
        {
            serviceId = await _services.Add(service);
            employeeId = await _users.Add(employee with { Role = UserRole.Employee });
            await _sut.Assign(serviceId, [employeeId]);
            actual = await _services.GetById(serviceId);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Equal([employeeId], actual!.Employees);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_ExistingAssignment_WHEN_AssignWithSubset_THEN_RemovedEmployeeIsNoLongerAssigned(
        Service service, User employee1, User employee2)
    {
        int serviceId;
        int employeeId1;
        Service? actual;
        await _transaction.Begin();
        try
        {
            serviceId = await _services.Add(service);
            employeeId1 = await _users.Add(employee1 with { Role = UserRole.Employee });
            int employeeId2 = await _users.Add(employee2 with { Role = UserRole.Employee });
            await _sut.Assign(serviceId, [employeeId1, employeeId2]);
            await _sut.Assign(serviceId, [employeeId1]);
            actual = await _services.GetById(serviceId);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Equal([employeeId1], actual!.Employees);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_ExistingAssignment_WHEN_AssignSameEmployees_THEN_Succeeds(Service service, User employee)
    {
        int serviceId;
        int employeeId;
        Service? actual;
        await _transaction.Begin();
        try
        {
            serviceId = await _services.Add(service);
            employeeId = await _users.Add(employee with { Role = UserRole.Employee });
            await _sut.Assign(serviceId, [employeeId]);
            await _sut.Assign(serviceId, [employeeId]);
            actual = await _services.GetById(serviceId);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Equal([employeeId], actual!.Employees);
    }
}
