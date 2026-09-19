using Klinkby.Booqr.Core;
using Moq;
using Activity = Klinkby.Booqr.Core.Activity;

namespace Klinkby.Booqr.Application.Tests;

public class ActivityRecorderTests
{
    private readonly Mock<IActivityRepository> _activities = new();

    [Theory]
    [InlineAutoData(nameof(ActivityRecorder.Add))]
    [InlineAutoData(nameof(ActivityRecorder.Update))]
    [InlineAutoData(nameof(ActivityRecorder.Delete))]
    public async Task Record_WritesActivityViaRepository(string activityName, ActivityQuery<PageQuery> query)
    {
        // Arrange
        Activity? recorded = null;
        _activities
            .Setup(r => r.Record(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
            .Callback<Activity, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);
        ActivityRecorder sut = new(_activities.Object, TestHelpers.TimeProvider);

        // Act
        await (Task)typeof(ActivityRecorder).GetMethod(activityName)!
            .MakeGenericMethod(typeof(PageQuery))
            .Invoke(sut, [query, CancellationToken.None])!;

        // Assert
        _activities.Verify(r => r.Record(It.IsAny<Activity>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(recorded);
        Assert.Equal(query.UserId, recorded.UserId);
        Assert.Equal(query.EntityId, recorded.EntityId);
        Assert.Equal(nameof(PageQuery), recorded.Entity);
        Assert.Equal(activityName, recorded.Action);
    }
}
