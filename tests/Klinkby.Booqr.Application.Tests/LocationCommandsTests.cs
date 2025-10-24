using Klinkby.Booqr.Application.Abstractions;

namespace Klinkby.Booqr.Application.Tests;

public class LocationCommandsTests
{
    private const int ExpectedId = 42;
    private readonly Mock<IETagProvider> _mockEtag = new();
    private readonly Mock<ILocationRepository> _mockRepo = CreateMockLocationRepository();

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AddLocationRequest_WHEN_Execute_THEN_LocationIsAddedToRepository(
        Location location,
        ClaimsPrincipal user)
    {
        AddLocationCommand command = new(_mockRepo.Object, NullLogger<AddLocationCommand>.Instance);
        AddLocationRequest request = new(location.Name) { User = user };

        var newId = await command.Execute(request);

        Assert.Equal(ExpectedId, newId);
        _mockRepo.Verify(x => x.Add(It.Is<Location>(l => l.Name == location.Name), CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UpdateLocationRequest_WHEN_Execute_THEN_LocationIsUpdatedInRepository(
        Location location,
        ClaimsPrincipal user)
    {
        _mockRepo.Setup(x => x.Update(It.IsAny<Location>(), CancellationToken.None)).ReturnsAsync(true);

        UpdateLocationCommand command = new(_mockRepo.Object, _mockEtag.Object,
            NullLogger<UpdateLocationCommand>.Instance);
        UpdateLocationRequest request = new(ExpectedId, location.Name) { User = user };

        await command.Execute(request);

        _mockRepo.Verify(
            x => x.Update(It.Is<Location>(l => l.Name == location.Name && l.Id == ExpectedId), CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_DeleteLocationRequest_WHEN_Execute_THEN_LocationIsDeletedInRepository(
        ClaimsPrincipal user)
    {
        _mockRepo.Setup(x => x.Delete(ExpectedId, CancellationToken.None)).ReturnsAsync(true);

        DeleteLocationCommand command = new(_mockRepo.Object, NullLogger<DeleteLocationCommand>.Instance);
        AuthenticatedByIdRequest request = new(ExpectedId) { User = user };

        await command.Execute(request);

        _mockRepo.Verify(x => x.Delete(ExpectedId, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_GetByIdRequest_WHEN_Execute_THEN_LocationIsReturnedFromRepository(
        Location location)
    {
        _mockRepo.Setup(x => x.GetById(ExpectedId, CancellationToken.None)).ReturnsAsync(location);

        GetLocationByIdCommand command = new(_mockRepo.Object);
        ByIdRequest request = new() { Id = ExpectedId };

        Location? result = await command.Execute(request);

        Assert.Equal(location, result);
        _mockRepo.Verify(x => x.GetById(ExpectedId, CancellationToken.None), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_GetAllLocationsRequest_WHEN_Execute_THEN_LocationsAreReturnedFromRepository(
        Location l1, Location l2)
    {
        Location[] expected = new[] { l1, l2 };
        _mockRepo.Setup(x => x.GetAll(It.IsAny<IPageQuery>(), default))
            .Returns(expected.ToAsyncEnumerable());

        GetLocationCollectionCommand command = new(_mockRepo.Object);
        PageQuery request = new();

        IAsyncEnumerable<Location> result = command.Execute(request);

        Location[] items = await result.ToArrayAsync();

        Assert.Equal(expected, items);
        _mockRepo.Verify(
            x => x.GetAll(It.Is<IPageQuery>(pq => pq.Start == request.Start && pq.Num == request.Num), default),
            Times.Once);
    }

    private static Mock<ILocationRepository> CreateMockLocationRepository()
    {
        var mockRepo = new Mock<ILocationRepository>();
        mockRepo.Setup(x => x
                .Add(It.IsAny<Location>(), CancellationToken.None))
            .ReturnsAsync(ExpectedId);
        return mockRepo;
    }
}
