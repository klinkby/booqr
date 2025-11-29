using Klinkby.Booqr.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests;

public class ReminderMailServiceTest
{
    [Theory]
    [AutoData]
    public async Task GIVEN_BookingDetails_WHEN_CronTriggers_THEN_MailIsSent(BookingDetails[] bookingDetails)
    {
        // arrange
        FakeTimeProvider timeProvider = TestHelpers.TimeProvider;
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        ServiceCollection services = CreateServiceCollection();
        using CountdownEvent cde = new(bookingDetails.Length);

        Mock<IMailClient> mailClientMock = new();
        mailClientMock
            .Setup(m => m.Send(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => cde.Signal())
            .Verifiable(Times.Exactly(bookingDetails.Length));
        services.AddSingleton(mailClientMock.Object);

        Mock<IBookingDetailsRepository> repoMock = new();
        repoMock
            .Setup(m => m.GetRange(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => bookingDetails.ToAsyncEnumerable())
            .Verifiable(Times.Once);
        services.AddSingleton(repoMock.Object);
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var settings = new ReminderMailSettings
            {
                // Give the background service ample time to start before the scheduled trigger
                // A larger offset improves stability on slower CI agents
                TimeOfDay = (now + TimeSpan.FromSeconds(5)).TimeOfDay
            };

        // act
        using var sut = new ReminderMailService(
            timeProvider,
            serviceProvider,
            Options.Create(settings),
            NullLogger<ReminderMailService>.Instance);
        await sut.StartAsync(CancellationToken.None);
        cde.Wait(TimeSpan.FromSeconds(20));
        await sut.StopAsync(CancellationToken.None);

        // assert
        mailClientMock.Verify();
        repoMock.Verify();
    }

    private static ServiceCollection CreateServiceCollection()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILogger<GetBookingDetailsCommand>>(NullLogger<GetBookingDetailsCommand>.Instance);
        services.AddTransient<GetBookingDetailsCommand>();
        return services;
    }

    [Theory]
    [InlineData(-1.0, 1.0)]
    [InlineData(0.0, 24.0)]
    [InlineData(1.0, 23.0)]
    public void GIVEN_GetNext_WHEN_Offset_THEN_NextDay(int offset, int expectedHours)
    {
        // arrange
        FakeTimeProvider timeProvider = TestHelpers.TimeProvider;
        var timeOfDay = TimeSpan.FromHours(12);
        timeProvider.Advance(timeOfDay);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime.AddHours(offset);
        var settings = new ReminderMailSettings { TimeOfDay = timeOfDay };
        ServiceCollection services = CreateServiceCollection();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        // act
        using var sut = new ReminderMailService(
            timeProvider,
            serviceProvider,
            Options.Create(settings),
            NullLogger<ReminderMailService>.Instance);


        DateTime next = sut.GetNext(now);

        // assert
        Assert.Equal(expectedHours, (next - now).TotalHours);
    }
}
