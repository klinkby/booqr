using System.Threading.Channels;
using Klinkby.Booqr.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Application.Tests.Services;

public class ActivityBackgroundServiceTests
{
    [Theory]
    [AutoData]
    public async Task GIVEN_ChannelWithActivity_WHEN_Started_THEN_CallsRepositoryAdd(Activity activity)
    {
        // Arrange
        var channel = Channel.CreateUnbounded<Activity>();
        Mock<IActivityRepository> repoMock = CreateRepositoryMock(activity);

        var services = new ServiceCollection();
        services.AddScoped<IActivityRepository>(_ => repoMock.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        using var sut =
            new ActivityBackgroundService(channel.Reader, provider, NullLogger<ActivityBackgroundService>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await sut.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(activity, cts.Token);
        channel.Writer.Complete();

        // Assert
        await Task.Delay(200, cts.Token); // Allow processing
        repoMock.Verify(r => r.Add(activity, It.IsAny<CancellationToken>()), Times.Once);
        await sut.StopAsync(CancellationToken.None);
    }

    private static Mock<IActivityRepository> CreateRepositoryMock(Activity activity)
    {
        var mock = new Mock<IActivityRepository>(MockBehavior.Strict);
        mock
            .Setup(r => r.Add(activity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return mock;
    }
}
