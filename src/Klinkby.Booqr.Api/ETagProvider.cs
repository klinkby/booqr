using System.Globalization;

namespace Klinkby.Booqr.Api;

/// <summary>
///     Scoped service that provide the If-None-Match header value if its convertible to DateTime.
/// </summary>
internal sealed class ETagProvider : IETagProvider
{
    public DateTime? Version { get; set; }
}

/// <summary>
///     Read If-None-Match header and store in IETagProvider scoped service.
///     Checks controller response for ETag and respond Not-Modified if there's a match.
/// </summary>
internal sealed class ETagProviderEndPointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;
        DateTime? version = GetIfNoneMatchVersion(httpContext);

        var response = await next(context);
        if (response is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return null;
        }

        return TrySetETagResponse(httpContext.Response, response as Audit, version) ? null : response;
    }

    private static DateTime? GetIfNoneMatchVersion(HttpContext httpContext)
    {
        DateTime? version = null;
        if (long.TryParse(httpContext.Request.Headers.IfNoneMatch, CultureInfo.InvariantCulture, out var ticks))
        {
            // https://ercanerdogan.medium.com/using-scoped-services-in-middleware-pitfalls-solutions-and-testing-in-asp-net-core-b79871ea0999
            ((ETagProvider)httpContext.RequestServices.GetRequiredService<IETagProvider>()).Version =
                version =
                    new DateTime(ticks, DateTimeKind.Utc);
        }

        return version;
    }

    private static bool TrySetETagResponse(HttpResponse httpResponse, Audit? auditResponse, DateTime? version)
    {
        DateTime? etagValue = auditResponse?.Modified;
        if (etagValue is not null)
        {
            if (etagValue == version)
            {
                httpResponse.StatusCode = StatusCodes.Status304NotModified;
                return true;
            }

            httpResponse.Headers.Append("ETag", auditResponse!.ETag);
        }

        return false;
    }
}
