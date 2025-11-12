using System.Diagnostics;
using System.Globalization;
using Klinkby.Booqr.Application.Commands.Bookings;
using Klinkby.Booqr.Application.Util;
using Klinkby.Booqr.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Services;

/// <summary>
/// ReminderMailService is a background service that orchestrates the sending of reminder emails
/// based on a configured schedule (CRON).
/// </summary>
/// <remarks>
/// This service calculates the next scheduled time for sending reminder emails and ensures
/// scheduled tasks continue executing until explicitly stopped.
/// </remarks>
/// <param name="timeProvider">Provides the current time, used to determine the next scheduled execution.</param>
/// <param name="serviceProvider">Provides access to registered application services.</param>
/// <param name="reminderMailSettings">Settings that define the time of day for sending reminders.</param>
/// <param name="logger">Logger instance for logging service activity and errors.</param>
internal sealed partial class ReminderMailService(
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    IOptions<ReminderMailSettings> reminderMailSettings,
    ILogger<ReminderMailService> logger) : BackgroundService
{
    private readonly LoggerMessages _log = new(logger);
    private readonly TimeSpan _timeOfDay = reminderMailSettings.Value.TimeOfDay;

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    internal DateTime GetNext(DateTime now)
    {
        return now
                   .Date
                   .AddDays(now.TimeOfDay >= _timeOfDay ? 1 : 0)
               + _timeOfDay;
    }

    async protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime now = Now;
            DateTime next = GetNext(now);
            TimeSpan timeToNext = next - now;
            _log.Sleep(timeToNext, next);
            await Task.Delay(timeToNext, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var timestamp = DateOnly.FromDateTime(Now);
            using IDisposable? loggerScope = logger.BeginScope(new { TimeStamp = timestamp });
            await using AsyncServiceScope serviceScope = serviceProvider.CreateAsyncScope();
            var sw = Stopwatch.StartNew();
            var messageCount =
                await FetchBookingDetailsAndSendReminders(serviceScope.ServiceProvider, timestamp, _log, stoppingToken);
            if (messageCount == 0)
            {
                _log.NothingToSend();
            }
            else
            {
                _log.Complete(messageCount, sw.Elapsed);
            }
        }
    }

    async private static Task<int> FetchBookingDetailsAndSendReminders(
        IServiceProvider scopedServices,
        DateOnly timestamp,
        LoggerMessages log,
        CancellationToken cancellationToken)
    {
        GetBookingDetailsCommand command = scopedServices.GetRequiredService<GetBookingDetailsCommand>();
        IMailClient mailClient = scopedServices.GetRequiredService<IMailClient>();

        var messageCount = 0;
        IAsyncEnumerable<BookingDetails> items =
            command.Execute(new GetBookingDetailsRequest(timestamp), cancellationToken);
        await foreach (BookingDetails booking in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            Message message = ComposeMessage(booking);
            await TrySendReminder(message, mailClient, log, cancellationToken);
            Interlocked.Increment(ref messageCount);
        }

        return messageCount;
    }

    private static Message ComposeMessage(BookingDetails booking)
    {
        return EmbeddedResource.Properties_Reminder_handlebars.ComposeMessage(
            booking.CustomerEmail,
            StringResources.ReminderSubject,
            new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(CultureInfo.InvariantCulture),
                ["start"] = booking.StartTime.ToString("hh:mm d/M", CultureInfo.InvariantCulture),
                ["duration"] = booking.Duration.ToString("hh':'mm", CultureInfo.InvariantCulture),
                ["name"] = booking.CustomerName ?? booking.CustomerEmail,
                ["employee"] = booking.Employee ?? "?",
                ["location"] = booking.Location,
                ["service"] = booking.Service
            });
    }

    async private static Task TrySendReminder(Message message, IMailClient mailClient, LoggerMessages log,
        CancellationToken stoppingToken)
    {
        try
        {
            await mailClient.Send(message, stoppingToken);
        }
        catch (MailClientException ex)
        {
            log.SendFailed(ex, message.To, ex.Code, ex.Status, ex.Message);
        }
    }

    private sealed partial class LoggerMessages(ILogger<ReminderMailService> logger)
    {
        private readonly ILogger<ReminderMailService> _logger = logger;

        [LoggerMessage(1110, LogLevel.Warning, "Email send to {ToAddress} {Status} {Code} {Message}")]
        public partial void SendFailed(Exception ex, string toAddress, int code, string? status, string message);

        [LoggerMessage(1111, LogLevel.Information, "Sent {Count} reminder emails in {Elapsed}")]
        public partial void Complete(int count, TimeSpan elapsed);

        [LoggerMessage(1112, LogLevel.Information, "No reminder emails to send")]
        public partial void NothingToSend();

        [LoggerMessage(1113, LogLevel.Information, "Sleep for {Duration} until {Next}")]
        public partial void Sleep(TimeSpan duration, DateTime next);
    }
}
