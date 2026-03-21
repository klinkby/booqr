using Klinkby.Booqr.Application.Abstractions;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class ServiceCommandsTests
{
    private const int ExpectedId = 42;
    private readonly Mock<IRequestMetadata> _mockEtag = new();
    private readonly Mock<IServiceRepository> _mockServiceRepo = CreateMockServiceRepository();
    private readonly Mock<IEmployeeServiceRepository> _mockEmpSvcRepo = CreateMockEmployeeServiceRepository();
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AddServiceRequest_WHEN_Execute_THEN_ServiceIsAddedToRepository(
        Service service,
        ClaimsPrincipal user)
    {
        var mockTransaction = new Mock<ITransaction>();
        AddServiceCommand command = new(_mockServiceRepo.Object, _mockEmpSvcRepo.Object, mockTransaction.Object, _activityRecorder.Object, NullLogger<AddServiceCommand>.Instance);
        AddServiceRequest request = new(service.Name, service.Duration, service.Employees) { User = user };

        var newId = await command.Execute(request);

        Assert.Equal(ExpectedId, newId);
        _mockServiceRepo.Verify(x => x.Add(It.Is<Service>(l => l.Name == service.Name), CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UpdateServiceRequest_WHEN_Execute_THEN_ServiceIsUpdatedInRepository(
        Service service,
        ClaimsPrincipal user)
    {
        _mockServiceRepo.Setup(x => x.Update(It.IsAny<Service>(), CancellationToken.None)).ReturnsAsync(true);
        var mockTransaction = new Mock<ITransaction>();

        UpdateServiceCommand command = new(_mockServiceRepo.Object, _mockEmpSvcRepo.Object, mockTransaction.Object, _mockEtag.Object, _activityRecorder.Object,
            NullLogger<UpdateServiceCommand>.Instance);
        UpdateServiceRequest request = new(ExpectedId, service.Name, service.Duration, service.Employees) { User = user };

        await command.Execute(request);

        _mockServiceRepo.Verify(
            x => x.Update(It.Is<Service>(s => s.Name == service.Name && s.Id == ExpectedId), CancellationToken.None),
            Times.Once);
        _mockEmpSvcRepo.Verify(
            x => x.Assign(
                It.Is<int>(s => s == ExpectedId),
                It.Is<int[]>(s => s.Length == service.Employees.Length), CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_GetByIdRequest_WHEN_Execute_THEN_ServiceIsReturnedFromRepository(
        Service service)
    {
        _mockServiceRepo.Setup(x => x.GetById(ExpectedId, CancellationToken.None)).ReturnsAsync(service);

        GetServiceByIdCommand command = new(_mockServiceRepo.Object);
        ByIdRequest request = new() { Id = ExpectedId };

        Service? result = await command.Execute(request);

        Assert.Equal(service, result);
        _mockServiceRepo.Verify(x => x.GetById(ExpectedId, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_GetAllServicesRequest_WHEN_Execute_THEN_ServicesAreReturnedFromRepository(
        Service l1, Service l2)
    {
        Service[] expected = new[] { l1, l2 };
        _mockServiceRepo.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), default))
            .Returns(expected.ToAsyncEnumerable());

        GetServiceCollectionCommand command = new(_mockServiceRepo.Object);
        PageQuery request = new();

        IAsyncEnumerable<Service> result = command.Execute(request);

        Service[] items = await result.ToArrayAsync();

        Assert.Equal(expected, items);
        _mockServiceRepo.Verify(
            x => x.GetAll(It.Is<IPageQuery>(pq => pq.Start == request.Start && pq.Num == request.Num), default),
            Times.Once);
    }

    private static Mock<IServiceRepository> CreateMockServiceRepository()
    {
        var mockRepo = new Mock<IServiceRepository>();
        mockRepo.Setup(x => x
                .Add(It.IsAny<Service>(), CancellationToken.None))
            .ReturnsAsync(ExpectedId);
        return mockRepo;
    }

    private static Mock<IEmployeeServiceRepository> CreateMockEmployeeServiceRepository()
    {
        var mockRepo = new Mock<IEmployeeServiceRepository>();
        mockRepo.Setup(x =>
            x.Assign(
                It.IsAny<int>(),
                It.IsAny<int[]>(),
                It.IsAny<CancellationToken>()));
        return mockRepo;
    }
}
