namespace Klinkby.Booqr.Core;

public interface IMailClient
{
    Task Send(Message message, CancellationToken cancellationToken = default);
}
