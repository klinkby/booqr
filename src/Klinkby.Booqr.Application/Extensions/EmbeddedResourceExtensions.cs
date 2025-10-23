using System.Text;

namespace Klinkby.Booqr.Application.Extensions;

internal static class EmbeddedResourceExtensions
{
    internal static Message CreateMessage(this EmbeddedResource resource, string email, string password, string subject) =>
        Message.From(
            email,
            subject,
            Handlebars.Replace(
                resource.ReadToEnd(),
                new Dictionary<string, string>
                {
                    ["email"] = email,
                    ["password"] = password
                }));

    private static string ReadToEnd(this EmbeddedResource embeddedResource)
    {
        using Stream stream = embeddedResource.GetStream();
        using StreamReader reader = new(stream, Encoding.UTF8, true, 1024, true);
        return reader.ReadToEnd();
    }
}
