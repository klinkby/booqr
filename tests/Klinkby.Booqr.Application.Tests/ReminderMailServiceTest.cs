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
        FakeTimeProvider timeProvider = new();
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        ServiceCollection services = CreateServiceCollection();
        using ManualResetEventSlim mre = new();

        Mock<IMailClient> mailClientMock = new();
        mailClientMock
            .Setup(m => m.Send(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => mre.Set())
            .Verifiable(Times.Exactly(bookingDetails.Length));
        services.AddSingleton(mailClientMock.Object);

        DateTime date = now.Date;
        Mock<IBookingDetailsRepository> repoMock = new();
        repoMock
            .Setup(m => m.GetRange(It.Is<DateTime>(x => x == date), It.Is<DateTime>(x => x == date.AddDays(1)),
                It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => bookingDetails.ToAsyncEnumerable())
            .Verifiable(Times.Once);
        services.AddSingleton(repoMock.Object);
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var settings = new ReminderMailSettings
            { TimeOfDay = (now + TimeSpan.FromMilliseconds(200)).TimeOfDay };

        // act
        using var sut = new ReminderMailService(
            timeProvider,
            serviceProvider,
            Options.Create(settings),
            NullLogger<ReminderMailService>.Instance);
        await sut.StartAsync(CancellationToken.None);
        mre.Wait(TimeSpan.FromSeconds(5));
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
        TimeSpan timeOfDay = TimeSpan.FromHours(12);
        FakeTimeProvider timeProvider = new(DateTimeOffset.UnixEpoch.UtcDateTime + timeOfDay);
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
