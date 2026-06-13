namespace Klinkby.Booqr.Core.Exceptions;

/// <summary>
///     Thrown when an authenticated request's identity claim is missing or unparseable,
///     indicating the bearer token does not carry a valid subject identifier.
/// </summary>
public sealed class InvalidClaimException : InvalidOperationException
{
    public InvalidClaimException()
    {
    }

    public InvalidClaimException(string message) : base(message)
    {
    }

    public InvalidClaimException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
