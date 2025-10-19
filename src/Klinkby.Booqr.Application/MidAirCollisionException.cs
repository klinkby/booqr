namespace Klinkby.Booqr.Application;

public class MidAirCollisionException : Exception
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
