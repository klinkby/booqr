namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents an email message with recipient, subject, and body content.
/// </summary>
/// <param name="Id">The unique identifier for the message.</param>
/// <param name="To">The recipient email address.</param>
/// <param name="Subject">The subject line of the email.</param>
/// <param name="Body">The body content of the email.</param>
public sealed record Message(Guid Id, string To, string Subject, string Body)
{
    /// <summary>
    ///     Creates a new email message with a generated unique identifier.
    /// </summary>
    /// <param name="to">The recipient email address.</param>
    /// <param name="subject">The subject line of the email.</param>
    /// <param name="body">The body content of the email.</param>
    /// <returns>A new <see cref="Message"/> instance with a generated <see cref="Guid"/>.</returns>
    public static Message From(string to, string subject, string body)
     =>  new(Guid.NewGuid(), to, subject, body);
}
