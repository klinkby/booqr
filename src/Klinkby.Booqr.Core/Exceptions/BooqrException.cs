namespace Klinkby.Booqr.Core.Exceptions;

/// <summary>
///     Base exception for all custom Booqr exceptions.
/// </summary>
public abstract class BooqrException : Exception
{
    protected BooqrException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected BooqrException()
    {
    }

    protected BooqrException(string message) : base(message)
    {
    }
}
