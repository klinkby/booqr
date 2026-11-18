namespace Klinkby.Booqr.Application.Tests.Commands;

public class GetActivityCollectionCommandTests
{
    private readonly Mock<IActivityRepository> _activities = new();

    private GetActivityCollectionCommand CreateSut()
    {
        return new GetActivityCollectionCommand(_activities.Object, TestHelpers.TimeProvider);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_PageQueryAndRange_WHEN_Execute_THEN_CallsRepository_And_ReturnsItems(
        DateTime t0, Activity a1, Activity a2, int start, int num)
    {
        // Arrange
        var page = new GetActivitiesRequest(t0.AddDays(-2), t0.AddDays(2)) { Start = start, Num = num };
        Activity[] expected = [a1, a2];

        _activities.Setup(x =>
                x.GetRange(page.FromTime!.Value, page.ToTime!.Value, page, It.IsAny<CancellationToken>()))
            .Returns(Yield(expected));

        GetActivityCollectionCommand sut = CreateSut();

        // Act
        var list = await sut.Execute(page);

        // Assert
        Assert.Equal(expected, list);
        _activities.Verify(
            x => x.GetRange(page.FromTime!.Value, page.ToTime!.Value, page, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NullFromAndTo_WHEN_Execute_THEN_DefaultsApplied(DateTime t0)
    {
        // Arrange
        var page = new GetActivitiesRequest(null, null);

        _activities.Setup(x => x.GetRange(It.IsAny<DateTime>(), It.IsAny<DateTime>(), page,
                It.IsAny<CancellationToken>()))
            .Returns(Yield<Activity>());

        GetActivityCollectionCommand sut = CreateSut();

        // Act
        await sut.Execute(page);

        // Assert
        _activities.Verify(x => x.GetRange(
            t0.AddDays(-1),
            DateTime.MaxValue,
            page,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        GetActivityCollectionCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.Execute(null!));
    }
}
