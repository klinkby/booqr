using Klinkby.Booqr.Application.Abstractions;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class ServiceCommandsTests
{
    private const int ExpectedId = 42;
    private readonly Mock<IRequestMetadata> _mockEtag = new();
    private readonly Mock<IServiceRepository> _mockRepo = CreateMockServiceRepository();
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AddServiceRequest_WHEN_Execute_THEN_ServiceIsAddedToRepository(
        Service service,
        ClaimsPrincipal user)
    {
        AddServiceCommand command = new(_mockRepo.Object, _activityRecorder.Object, NullLogger<AddServiceCommand>.Instance);
        AddServiceRequest request = new(service.Name, service.Duration) { User = user };

        var newId = await command.Execute(request);

        Assert.Equal(ExpectedId, newId);
        _mockRepo.Verify(x => x.Add(It.Is<Service>(l => l.Name == service.Name), CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UpdateServiceRequest_WHEN_Execute_THEN_ServiceIsUpdatedInRepository(
        Service service,
        ClaimsPrincipal user)
    {
        _mockRepo.Setup(x => x.Update(It.IsAny<Service>(), CancellationToken.None)).ReturnsAsync(true);

        UpdateServiceCommand command = new(_mockRepo.Object, _mockEtag.Object, _activityRecorder.Object,
            NullLogger<UpdateServiceCommand>.Instance);
        UpdateServiceRequest request = new(ExpectedId, service.Name, service.Duration) { User = user };

        await command.Execute(request);

        _mockRepo.Verify(
            x => x.Update(It.Is<Service>(s => s.Name == service.Name && s.Id == ExpectedId), CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_DeleteServiceRequest_WHEN_Execute_THEN_ServiceIsDeletedInRepository(
        ClaimsPrincipal user)
    {
        _mockRepo.Setup(x => x.Delete(ExpectedId, CancellationToken.None)).ReturnsAsync(true);

        DeleteServiceCommand command = new(_mockRepo.Object, _activityRecorder.Object, NullLogger<DeleteServiceCommand>.Instance);
        AuthenticatedByIdRequest request = new(ExpectedId) { User = user };

        await command.Execute(request);

        _mockRepo.Verify(x => x.Delete(ExpectedId, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_GetByIdRequest_WHEN_Execute_THEN_ServiceIsReturnedFromRepository(
        Service service)
    {
        _mockRepo.Setup(x => x.GetById(ExpectedId, CancellationToken.None)).ReturnsAsync(service);

        GetServiceByIdCommand command = new(_mockRepo.Object);
        ByIdRequest request = new() { Id = ExpectedId };

        Service? result = await command.Execute(request);

        Assert.Equal(service, result);
        _mockRepo.Verify(x => x.GetById(ExpectedId, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_GetAllServicesRequest_WHEN_Execute_THEN_ServicesAreReturnedFromRepository(
        Service l1, Service l2)
    {
        Service[] expected = new[] { l1, l2 };
        _mockRepo.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), default))
            .Returns(expected.ToAsyncEnumerable());

        GetServiceCollectionCommand command = new(_mockRepo.Object);
        PageQuery request = new();

        IAsyncEnumerable<Service> result = command.Execute(request);

        Service[] items = await result.ToArrayAsync();

        Assert.Equal(expected, items);
        _mockRepo.Verify(
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
}
