using System.Threading.Channels;
using Activity = Klinkby.Booqr.Core.Activity;

namespace Klinkby.Booqr.Application.Tests;

public class ActivityRecorderTests
{
    private readonly Channel<Activity> _channel = Channel.CreateUnbounded<Activity>();

    [Theory]
    [InlineAutoData(nameof(ActivityRecorder.Add))]
    [InlineAutoData(nameof(ActivityRecorder.Update))]
    [InlineAutoData(nameof(ActivityRecorder.Delete))]
    public void Add_WritesActivityToChannel(string activityName, ActivityQuery<PageQuery> query)
    {
        // Arrange
        ActivityRecorder sut = new(_channel.Writer, TestHelpers.TimeProvider);

        // Act
        typeof(ActivityRecorder).GetMethod(activityName)!
            .MakeGenericMethod(typeof(PageQuery))
            .Invoke(sut, [query]);

        // Assert
        Assert.True(_channel.Reader.TryRead(out var activity));
        Assert.Equal(query.UserId, activity.UserId);
        Assert.Equal(query.EntityId, activity.EntityId);
        Assert.Equal(nameof(PageQuery), activity.Entity);
        Assert.Equal(activityName, activity.Action);
    }
}
