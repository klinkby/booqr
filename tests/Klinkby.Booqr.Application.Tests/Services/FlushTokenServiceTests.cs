using System.Globalization;
using Klinkby.Booqr.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests.Services;

public class FlushTokenServiceTests
{
    [Theory]
    [AutoData]
    public async Task GIVEN_ExpiredTokens_WHEN_ServiceRuns_THEN_TokensAreDeleted(int deletedCount)
    {
        // arrange
        FakeTimeProvider timeProvider = TestHelpers.TimeProvider;
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        // The service triggers at midnight (TimeSpan.Zero).
        // We set the time to just before midnight to trigger it quickly after start.
        timeProvider.SetUtcNow(new DateTimeOffset(now.Date.AddHours(23).AddMinutes(59).AddSeconds(55), TimeSpan.Zero));
        now = timeProvider.GetUtcNow().UtcDateTime;

        ServiceCollection services = new();
        Mock<IRefreshTokenRepository> repoMock = new();
        repoMock
            .Setup(m => m.Delete(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount)
            .Verifiable(Times.Once);
        services.AddSingleton(repoMock.Object);

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using CountdownEvent cde = new(1);

        // We need to signal when the task is done. Since FlushTokenService doesn't have a callback,
        // we can wrap the repository call or just use the repoMock's callback.
        repoMock.Setup(m => m.Delete(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount)
            .Callback(() => cde.Signal())
            .Verifiable(Times.Once);

        // act
        using var sut = new FlushTokenService(
            timeProvider,
            serviceProvider,
            NullLogger<FlushTokenService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        // Advance time to trigger the service (it triggers at midnight)
        // ScheduleBackgroundService calculates delay once at the start of loop.
        // We need to wait a bit for it to enter the Delay call.
        await Task.Delay(100);
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        bool signaled = cde.Wait(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None);

        // assert
        Assert.True(signaled, "The scheduled task was not executed.");
        repoMock.Verify(m => m.Delete(
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_RepositoryThrows_WHEN_ServiceRuns_THEN_ExceptionIsCaught()
    {
        // arrange
        FakeTimeProvider timeProvider = TestHelpers.TimeProvider;
        timeProvider.SetUtcNow(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(23).AddMinutes(59).AddSeconds(55), TimeSpan.Zero));

        ServiceCollection services = new();
        Mock<IRefreshTokenRepository> repoMock = new();
        using CountdownEvent cde = new(1);
        repoMock
            .Setup(m => m.Delete(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"))
            .Callback(() => cde.Signal());
        services.AddSingleton(repoMock.Object);

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        // act
        using var sut = new FlushTokenService(
            timeProvider,
            serviceProvider,
            NullLogger<FlushTokenService>.Instance);

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        bool signaled = cde.Wait(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None);

        // assert
        Assert.True(signaled);
        repoMock.Verify(m => m.Delete(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("2023-01-01T12:00:00", "2023-01-02T00:00:00")] // Afternoon -> Next midnight
    [InlineData("2023-01-01T00:00:00", "2023-01-02T00:00:00")] // Midnight -> Next midnight (because TriggerTimeOfDay is Zero and now.TimeOfDay >= TriggerTimeOfDay is true)
    [InlineData("2023-01-01T23:59:59", "2023-01-02T00:00:00")] // Just before midnight -> Next midnight
    public void GIVEN_GetNext_WHEN_Now_THEN_CorrectNextMidnight(string nowStr, string expectedStr)
    {
        // arrange
        FakeTimeProvider timeProvider = TestHelpers.TimeProvider;
        var now = DateTime.Parse(nowStr, CultureInfo.InvariantCulture);
        var expected = DateTime.Parse(expectedStr, CultureInfo.InvariantCulture);

        ServiceCollection services = new();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        // act
        using var sut = new FlushTokenService(
            timeProvider,
            serviceProvider,
            NullLogger<FlushTokenService>.Instance);

        DateTime next = sut.GetNext(now);

        // assert
        Assert.Equal(expected, next);
    }
}
