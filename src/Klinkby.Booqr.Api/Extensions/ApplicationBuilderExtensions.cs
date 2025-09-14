namespace Klinkby.Booqr.Api.Extensions;

internal static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            HttpResponse res = context.Response;
            res.OnStarting(_ =>
                {
                    IHeaderDictionary headers = res.Headers;
                    // https://developer.mozilla.org/en-US/observatory/docs/faq#can_i_scan_non-websites_such_as_api_endpoints
                    headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
                    headers.Append("Strict-Transport-Security", "max-age=63072000");
                    headers.Append("X-Content-Type-Options", "nosniff");

                    headers.Append("Server", "Booqr");
                    return Task.CompletedTask;
                },
                context);
            await next();
        });
}
