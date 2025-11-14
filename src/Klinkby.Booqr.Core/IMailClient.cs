namespace Klinkby.Booqr.Core;

/// <summary>
///     Provides an interface for sending email messages.
/// </summary>
/// <remarks>
///     Implementations of this interface handle the delivery of email messages through various mail services.
/// </remarks>
public interface IMailClient
{
    /// <summary>
    ///     Sends an email message asynchronously.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="Exceptions.MailClientException">Thrown when the email fails to send.</exception>
    Task Send(Message message, CancellationToken cancellationToken = default);
}
