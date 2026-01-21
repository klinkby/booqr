using System.Diagnostics;
using System.Globalization;
using Klinkby.Booqr.Application.Commands.Bookings;
using Klinkby.Booqr.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
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
    ILogger<ReminderMailService> logger) : ScheduledBackgroundService(timeProvider, logger)
{
    private readonly LoggerMessages _log = new(logger);
    protected override TimeSpan TriggerTimeOfDay => reminderMailSettings.Value.TimeOfDay;

    protected override async Task ExecuteScheduledTaskAsync(CancellationToken stoppingToken)
    {
        var timestamp = DateOnly.FromDateTime(Now);
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

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _log.ReminderMailServiceStart(TriggerTimeOfDay);
        await base.StartAsync(cancellationToken);
    }

    private static async Task<int> FetchBookingDetailsAndSendReminders(
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

    private static async Task TrySendReminder(Message message, IMailClient mailClient, LoggerMessages log,
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

        [LoggerMessage(270, LogLevel.Warning, "Email send to {ToAddress} {Status} {Code} {Message}")]
        public partial void SendFailed(Exception ex, string toAddress, int code, string? status, string message);

        [LoggerMessage(271, LogLevel.Information, "Sent {Count} reminder emails in {Elapsed}")]
        public partial void Complete(int count, TimeSpan elapsed);

        [LoggerMessage(272, LogLevel.Information, "No reminder emails to send")]
        public partial void NothingToSend();

        [LoggerMessage(274, LogLevel.Information, "Reminder emails is sent at {TimeOfDay}")]
        public partial void ReminderMailServiceStart(TimeSpan timeOfDay);
    }
}
