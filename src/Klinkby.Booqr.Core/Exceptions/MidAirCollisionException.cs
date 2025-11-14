namespace Klinkby.Booqr.Core.Exceptions;

/// <summary>
///     Represents a concurrency conflict that occurs when attempting to update data that has been
///     modified by another user or process since it was retrieved.
/// </summary>
/// <remarks>
///     This exception is thrown during optimistic concurrency control scenarios when a data modification
///     operation fails because the data has changed since it was last read. This is commonly known as
///     a "mid-air collision" in distributed systems, where two parties attempt to update the same resource
///     simultaneously without coordination.
/// </remarks>
public class MidAirCollisionException : BooqrException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MidAirCollisionException"/> class.
    /// </summary>
    public MidAirCollisionException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MidAirCollisionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public MidAirCollisionException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MidAirCollisionException"/> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MidAirCollisionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
