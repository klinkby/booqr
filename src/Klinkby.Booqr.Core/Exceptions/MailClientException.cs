namespace Klinkby.Booqr.Core.Exceptions;

/// <summary>
///     Represents errors that occur during email client operations.
/// </summary>
/// <remarks>
///     This exception is thrown when the mail client encounters errors while attempting to send
///     email messages. It can include additional status information and error codes from the
///     underlying mail service.
/// </remarks>
public class MailClientException : BooqrException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MailClientException"/> class with detailed error information.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="status">The status description from the mail service.</param>
    /// <param name="code">The error code from the mail service.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MailClientException(string message, string status, int code, Exception innerException)
        : base(message, innerException)
    {
        Status = status;
        Code = code;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MailClientException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public MailClientException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MailClientException"/> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MailClientException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MailClientException"/> class.
    /// </summary>
    public MailClientException()
    {
    }

    /// <summary>
    ///     Gets the status description from the mail service, if available.
    /// </summary>
    /// <value>The status description, or <c>null</c> if not provided.</value>
    public string? Status { get; }

    /// <summary>
    ///     Gets the error code from the mail service.
    /// </summary>
    /// <value>The error code, or 0 if not provided.</value>
    public int Code { get; }
}
