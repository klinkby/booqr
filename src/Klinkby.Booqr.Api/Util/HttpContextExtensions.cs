using Klinkby.Booqr.Api.Models;
using Microsoft.Extensions.Options;

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
