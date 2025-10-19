namespace Klinkby.Booqr.Infrastructure.MailClient;

internal interface IMailClient
{
    Task Send(Message message, CancellationToken cancellationToken = default);
}
