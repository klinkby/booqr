using Microsoft.Extensions.Logging;

namespace Klinkby.Booqr.Infrastructure;

internal interface ISmtpClient
{
    Task Send(Message message, CancellationToken cancellationToken = default);
}

internal sealed partial class TransactionalMailerClient(ILogger<TransactionalMailerClient> logger) : ISmtpClient
{
    private readonly LoggerMessages _log = new(logger);

    public Task Send(Message message, CancellationToken cancellationToken = default)
    {
        _log.EmailPassword(message.To, message.Subject, message.Body);  // HACK
        return Task.CompletedTask;
    }

    private sealed partial class LoggerMessages(ILogger<TransactionalMailerClient> logger)
    {
        private readonly ILogger<TransactionalMailerClient> _logger = logger;

        [LoggerMessage(1020, LogLevel.Information, "To:{To} Subject:{Subject} Body:{Body}")]
        public partial void EmailPassword(string to, string subject, string body); // HACK
    }
}
