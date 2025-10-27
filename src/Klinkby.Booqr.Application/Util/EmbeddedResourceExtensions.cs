namespace Klinkby.Booqr.Application.Util;

internal static class EmbeddedResourceExtensions
{
    public static Message ComposeMessage(this EmbeddedResource template, string toEmail, string subject, Dictionary<string, string> replacements) =>
        Message.From(
            toEmail,
            subject,
            Handlebars.Replace(template.ReadToEnd(), replacements));

    private static string ReadToEnd(this EmbeddedResource embeddedResource)
    {
        using StreamReader reader = embeddedResource.GetReader();
        return reader.ReadToEnd();
    }
}
