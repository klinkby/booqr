namespace Klinkby.Booqr.Api.Util;

internal static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            HttpResponse res = context.Response;
            res.OnStarting(_ =>
                {
                    IHeaderDictionary headers = res.Headers;
                    headers.Append("X-Request-Id", context.TraceIdentifier);
                    // https://developer.mozilla.org/en-US/observatory/docs/faq#can_i_scan_non-websites_such_as_api_endpoints
                    headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
                    headers.Append("X-Content-Type-Options", "nosniff");
                    return Task.CompletedTask;
                },
                context);
            await next();
        });
}
