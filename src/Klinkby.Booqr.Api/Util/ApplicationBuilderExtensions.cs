namespace Klinkby.Booqr.Api.Util;

internal static class ApplicationBuilderExtensions
{
    private const string ContentSecurityPolicyValue = "default-src 'none'; frame-ancestors 'none'";
    private const string XContentTypeOptionsValue = "nosniff";

    internal static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            HttpResponse res = context.Response;
            res.OnStarting(_ =>
                {
                    IHeaderDictionary headers = res.Headers;
                    headers.Append("X-Request-Id", context.TraceIdentifier);
                    headers.Append("Content-Security-Policy", ContentSecurityPolicyValue);
                    headers.Append("X-Content-Type-Options", XContentTypeOptionsValue);
                    return Task.CompletedTask;
                },
                context);
            await next();
        });
}
