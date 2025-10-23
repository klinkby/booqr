namespace Klinkby.Booqr.Core;

public class MailClientException : Exception
{
    public MailClientException(string message, string status, int code, Exception innerException)
        : base(message, innerException)
    {
        Status = status;
        Code = code;
    }

    public MailClientException(string message) : base(message)
    {
    }

    public MailClientException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public MailClientException()
    {
    }

    public string? Status { get; }
    public int Code { get; }
}
