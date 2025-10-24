namespace Klinkby.Booqr.Core.Exceptions;

public class MidAirCollisionException : BooqrException
{
    public MidAirCollisionException()
    {
    }

    public MidAirCollisionException(string message) : base(message)
    {
    }

    public MidAirCollisionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
