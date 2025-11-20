namespace Klinkby.Booqr.Api.Util;

internal static class HttpContextExtensions
{
    extension(HttpContext context)
    {
        internal Uri ContextAuthority =>
            new(context.Request.Scheme + Uri.SchemeDelimiter + context.Request.Host.Value,
                UriKind.Absolute);
    }
}
