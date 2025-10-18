namespace Klinkby.Booqr.Core;

public sealed record Message(Guid Id, string To, string Subject, string Body)
{
    public static Message From(string to, string subject, string body)
     =>  new(Guid.NewGuid(), to, subject, body);
}
