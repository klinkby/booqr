namespace Klinkby.Booqr.Core.Exceptions;

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
