using System.Threading.Channels;
using Klinkby.Booqr.Infrastructure.MailClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Klinkby.Booqr.Infrastructure;

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
            try
            {
                await mailClient.Send(message, stoppingToken);
                _log.SendSuccess(message.To);
            }
            catch (MailClientException ex)
            {
                _log.SendFailed(ex, message.To, ex.Code, ex.Status, ex.Message);
            }
        }
    }

    private sealed partial class LoggerMessages(ILogger<EmailBackgroundService> logger)
    {
        private readonly ILogger<EmailBackgroundService> _logger = logger;

        [LoggerMessage(1010, LogLevel.Information, "Send mail to {ToAddress} succeeded")]
        public partial void SendSuccess(string toAddress);

        [LoggerMessage(1011, LogLevel.Warning, "Email send to {ToAddress} {Status} {Code} {Message}")]
        public partial void SendFailed(Exception ex, string toAddress, int code, string? status, string message);
    }
}
