namespace Klinkby.Booqr.Core.Exceptions;

/// <summary>
///     Serves as the base class for all application-specific exceptions in the Booqr system.
/// </summary>
/// <remarks>
///     This abstract class extends <see cref="Exception"/> and provides a common base for all custom
///     exceptions thrown by the Booqr application. It ensures consistent exception handling patterns
///     across the entire application domain.
/// </remarks>
public abstract class BooqrException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BooqrException"/> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected BooqrException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="BooqrException"/> class.
    /// </summary>
    protected BooqrException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="BooqrException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    protected BooqrException(string message) : base(message)
    {
    }
}
