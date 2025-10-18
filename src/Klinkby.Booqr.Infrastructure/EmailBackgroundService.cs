using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Klinkby.Booqr.Infrastructure;

internal sealed partial class EmailBackgroundService(
    ChannelReader<Message> reader,
    ISmtpClient smtpClient,
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
                await smtpClient.Send(message, stoppingToken);
                _log.SendSuccess(message.To);
            }
            catch (InvalidOperationException e) // TODO use specific
            {
                _log.SendFail(e, message.To);
            }
        }
    }

    private sealed partial class LoggerMessages(ILogger<EmailBackgroundService> logger)
    {
        private readonly ILogger<EmailBackgroundService> _logger = logger;

        [LoggerMessage(1010, LogLevel.Information, "Send mail to {ToAddress} succeeded")]
        public partial void SendSuccess(string toAddress);

        [LoggerMessage(1011, LogLevel.Warning, "Send mail to {ToAddress} failed")]
        public partial void SendFail(Exception e, string toAddress);
    }
}
