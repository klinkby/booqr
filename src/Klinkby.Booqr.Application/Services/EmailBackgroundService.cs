using System.Threading.Channels;
using Klinkby.Booqr.Core.Exceptions;
using Microsoft.Extensions.Hosting;

namespace Klinkby.Booqr.Application.Services;

internal sealed partial class EmailBackgroundService(
    ChannelReader<Message> reader,
    IMailClient mailClient,
    ILogger<EmailBackgroundService> logger) : BackgroundService
{
    private readonly LoggerMessages _log = new(logger);

    async protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Message message in reader.ReadAllAsync(stoppingToken))
        {
            using IDisposable? loggerScope = logger.BeginScope(new { MessageId = message.Id });
            await TrySendEmail(message, stoppingToken);
        }
    }

    async private Task TrySendEmail(Message message, CancellationToken stoppingToken)
    {
        try
        {
            await mailClient.Send(message, stoppingToken);
        }
        catch (MailClientException ex)
        {
            _log.SendFailed(ex, message.To, ex.Code, ex.Status, ex.Message);
        }
    }

    private sealed partial class LoggerMessages(ILogger<EmailBackgroundService> logger)
    {
        private readonly ILogger<EmailBackgroundService> _logger = logger;

        [LoggerMessage(260, LogLevel.Warning, "Email send to {ToAddress} {Status} {Code} {Message}")]
        public partial void SendFailed(Exception ex, string toAddress, int code, string? status, string message);
    }
}
