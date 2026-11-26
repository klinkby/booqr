using Klinkby.Booqr.Api.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Klinkby.Booqr.Api.Util;

internal static class HttpContextExtensions
{
    /// <summary>
    ///     Builds the authority signed links (sign-up activation, password reset) are issued against.
    /// </summary>
    /// <remarks>
    ///     Per docs/1-design.md §2a "Signed links": issued against the tenant's own subdomain
    ///     authority (<c>https://&lt;slug&gt;.booqr.dk</c>), not whatever scheme/host the request
    ///     happened to arrive on (HAProxy may terminate TLS and forward as <c>http</c>). Falls back to
    ///     the raw request authority only when no tenant was resolved — those endpoints (sign-up,
    ///     reset) are tenant-required in practice via RLS on the underlying user row, but the
    ///     tenant-resolution middleware itself does not error on an unresolved host, so this stays
    ///     defensive rather than throwing here. When a tenant is resolved the slug comes straight from
    ///     <see cref="ITenantContext.Slug" /> (set by the middleware) rather than being re-parsed.
    /// </remarks>
    /// <summary>Marks the response as not storable by any cache (signed links and credentials).</summary>
    internal static void SetNoStore(this HttpContext context) =>
        context.Response.Headers.CacheControl = "no-store";

    /// <summary>
    ///     Marks a response whose text was localized from <c>Accept-Language</c>: tells caches the body varies by it
    ///     and labels the language chosen. Call only for responses whose text came from the localized resources.
    /// </summary>
    internal static void SetContentLanguage(this HttpContext context)
    {
        if (context.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture.Name is not { Length: > 0 } language)
        {
            return;
        }

        IHeaderDictionary headers = context.Response.Headers;
        headers.Append(HeaderNames.Vary, HeaderNames.AcceptLanguage);
        headers.ContentLanguage = language;
    }

    /// <summary>
    ///     Writes <paramref name="problem" /> through <see cref="IProblemDetailsService" /> (content negotiation and
    ///     the central <see cref="ProblemDetailsCustomizer" /> hook). If every writer declines (e.g. <c>Accept: text/html</c>),
    ///     applies the same customization and forces <c>application/problem+json</c> AOT-safely.
    /// </summary>
    internal static async ValueTask WriteProblemAsync(this HttpContext context, ProblemDetails problem, CancellationToken cancellation)
    {
        ProblemDetailsContext problemContext = new() { HttpContext = context, ProblemDetails = problem };
        if (await context.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(problemContext))
        {
            return;
        }

        ProblemDetailsCustomizer.Customize(problemContext);
        await context.Response.WriteAsJsonAsync(
            problem, AppJsonSerializerContext.Default.ProblemDetails, "application/problem+json", cancellation);
    }

    internal static string GetContextAuthority(this HttpContext context)
    {
        ITenantContext tenantContext = context.RequestServices.GetRequiredService<ITenantContext>();
        if (!tenantContext.HasTenant)
        {
            return context.Request.Scheme + Uri.SchemeDelimiter + context.Request.Host.Value;
        }

        TenancySettings settings = context.RequestServices.GetRequiredService<IOptions<TenancySettings>>().Value;
        return $"https://{tenantContext.Slug}.{settings.BaseDomain}";
    }
}
