using Klinkby.Booqr.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests.Services;

public class ScheduledBackgroundServiceTests
{
    /// <summary>Concrete test double that records whether <see cref="ExecuteScheduledTaskAsync"/> was called.</summary>
    private sealed class TestScheduledService(
        TimeProvider timeProvider,
        IServiceProvider serviceProvider,
        CountdownEvent executed)
        : ScheduledBackgroundService(timeProvider, serviceProvider, NullLogger.Instance)
    {
        protected override TimeSpan TriggerTimeOfDay => TimeSpan.Zero;
        protected override string JobName => "test-job";

        protected override Task ExecuteScheduledTaskAsync(CancellationToken cancellation)
        {
            executed.Signal();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GIVEN_JobClaimed_WHEN_ScheduledTimeReached_THEN_TaskExecutes()
    {
        // arrange
        FakeTimeProvider timeProvider = TestHelpers.TimeProvider;
        timeProvider.SetUtcNow(new DateTimeOffset(
            DateTime.UtcNow.Date.AddHours(23).AddMinutes(59).AddSeconds(55), TimeSpan.Zero));

        ServiceCollection services = new();
        Mock<IJobClaim> jobClaimMock = new();
        jobClaimMock
            .Setup(m => m.TryClaimAsync("test-job", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable(Times.Once);
        services.AddSingleton(jobClaimMock.Object);
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        using CountdownEvent executed = new(1);
        using var sut = new TestScheduledService(timeProvider, serviceProvider, executed);

        // act
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        bool signaled = executed.Wait(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None);

        // assert
        Assert.True(signaled, "ExecuteScheduledTaskAsync should have been called when claim succeeds");
        jobClaimMock.Verify();
    }

    [Fact]
    public async Task GIVEN_JobAlreadyClaimed_WHEN_ScheduledTimeReached_THEN_TaskIsSkipped()
    {
        // arrange
        FakeTimeProvider timeProvider = TestHelpers.TimeProvider;
        timeProvider.SetUtcNow(new DateTimeOffset(
            DateTime.UtcNow.Date.AddHours(23).AddMinutes(59).AddSeconds(55), TimeSpan.Zero));

        ServiceCollection services = new();
        Mock<IJobClaim> jobClaimMock = new();
        using CountdownEvent claimAttempted = new(1);
        jobClaimMock
            .Setup(m => m.TryClaimAsync("test-job", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Callback(() => claimAttempted.Signal())
            .Verifiable(Times.Once);
        services.AddSingleton(jobClaimMock.Object);
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        using CountdownEvent executed = new(1);
        using var sut = new TestScheduledService(timeProvider, serviceProvider, executed);

        // act
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        bool claimed = claimAttempted.Wait(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None);

        // assert
        Assert.True(claimed, "TryClaimAsync should have been called");
        Assert.Equal(1, executed.CurrentCount); // countdown was never signaled
        jobClaimMock.Verify();
    }
}
