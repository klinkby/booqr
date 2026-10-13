using System.Collections.Concurrent;
using Klinkby.Booqr.Application.Util;
using Klinkby.Booqr.Application.Workers;
using Klinkby.Booqr.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests.Workers;

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
        ConcurrentQueue<Message> sent = new();

        Mock<IMailClient> mailClientMock = new();
        mailClientMock
            .Setup(m => m.Send(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<Message, CancellationToken>((message, _) =>
            {
                sent.Enqueue(message);
                cde.Signal();
            })
            .Verifiable(Times.Exactly(bookingDetails.Length));
        services.AddSingleton(mailClientMock.Object);

        Mock<IBookingDetailsRepository> repoMock = new();
        repoMock
            .Setup(m => m.GetRange(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => bookingDetails.ToAsyncEnumerable())
            .Verifiable(Times.Once);
        services.AddSingleton(repoMock.Object);

        Dictionary<int, string> tenantNames = bookingDetails
            .Select(b => b.TenantId)
            .Distinct()
            .ToDictionary(id => id, id => $"Tenant {id}");
        Mock<ITenantNameRepository> tenantNameMock = new();
        tenantNameMock
            .Setup(m => m.GetDisplayNames(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantNames);
        services.AddSingleton(tenantNameMock.Object);
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var settings = new ReminderMailSettings
            {
                // Give the background service ample time to start before the scheduled trigger
                // A larger offset improves stability on slower CI agents
                TimeOfDay = TimeZoneInfo.ConvertTimeFromUtc(now + TimeSpan.FromSeconds(5), MailTime.Copenhagen).TimeOfDay
            };

        // act
        using var sut = new ReminderMailService(
            timeProvider,
            serviceProvider,
            Options.Create(settings),
            NullLogger<ReminderMailService>.Instance);
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(100); // let ExecuteAsync reach Task.Delay
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        cde.Wait(TimeSpan.FromSeconds(20));
        await sut.StopAsync(CancellationToken.None);

        // assert
        mailClientMock.Verify();
        repoMock.Verify();
        Assert.All(bookingDetails, booking =>
            Assert.Contains(sent, m => m.To == booking.CustomerEmail
                                       && m.Body.Contains(tenantNames[booking.TenantId], StringComparison.InvariantCulture)));
    }

    private static ServiceCollection CreateServiceCollection()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILogger<GetBookingDetailsCommand>>(NullLogger<GetBookingDetailsCommand>.Instance);
        services.AddTransient<GetBookingDetailsCommand>();

        Mock<IJobClaim> jobClaimMock = new();
        jobClaimMock
            .Setup(m => m.TryClaimAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton(jobClaimMock.Object);
        services.AddScoped<IBatchScope, TestBatchScope>();

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
        timeProvider.Advance(TimeSpan.FromHours(12));
        // trigger time is Danish local time: use the current Danish time of day
        TimeSpan timeOfDay = TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, MailTime.Copenhagen).TimeOfDay;
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

    [Theory]
    [InlineData("2026-01-15T10:00:00", "2026-01-15T17:00:00")] // CET: 18:00 DK = 17:00 UTC
    [InlineData("2026-07-15T10:00:00", "2026-07-15T16:00:00")] // CEST: 18:00 DK = 16:00 UTC
    [InlineData("2026-07-15T16:00:00", "2026-07-16T16:00:00")] // exactly at trigger -> next day
    public void GIVEN_DefaultSettings_WHEN_GetNext_THEN_18DanishTimeInUtc(string nowStr, string expectedStr)
    {
        var now = DateTime.SpecifyKind(DateTime.Parse(nowStr, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        using ServiceProvider serviceProvider = CreateServiceCollection().BuildServiceProvider();
        using var sut = new ReminderMailService(
            TestHelpers.TimeProvider,
            serviceProvider,
            Options.Create(new ReminderMailSettings()),
            NullLogger<ReminderMailService>.Instance);

        Assert.Equal(DateTime.Parse(expectedStr, System.Globalization.CultureInfo.InvariantCulture), sut.GetNext(now));
    }
}
