using System.Text.RegularExpressions;
using Klinkby.Booqr.Core;
using Klinkby.Booqr.Infrastructure.Models;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Api.Util;

internal static partial class ApplicationBuilderExtensions
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

    /// <summary>
    ///     Resolves the tenant for the current request from <see cref="HttpRequest.Host" /> (never
    ///     <c>X-Forwarded-Host</c> — HAProxy preserves the authority) and populates the scoped
    ///     <see cref="IMutableTenantContext" />.
    /// </summary>
    /// <remarks>
    ///     Reserved subdomains and the apex (naked base domain) resolve to <em>no tenant</em> — not an
    ///     error, since they serve the marketing site. An unknown/deleted/malformed slug also leaves
    ///     the context empty rather than failing the request outright; endpoints that require a tenant
    ///     reject via <see cref="TenantRequiredEndPointFilter" /> (see <c>Routing.cs</c>), and
    ///     <c>GET /api/my-tenant</c> is the one place that turns "no tenant" into its own
    ///     <c>404 tenant-not-found</c>. Must run before <c>UseAuthorization</c> per docs/1-design.md
    ///     "Request flow", since authorization/endpoints depend on the resolved tenant and the scoped
    ///     tenant connection.
    /// </remarks>
    internal static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            TenancySettings settings = context.RequestServices
                .GetRequiredService<IOptions<TenancySettings>>().Value;
            var host = context.Request.Host.Host;

            var slug = ExtractTenantSlug(host, settings.BaseDomain, settings.ReservedSubdomains);
            if (slug is not null)
            {
                ITenantRepository tenantRepository = context.RequestServices.GetRequiredService<ITenantRepository>();
                Tenant? tenant = await tenantRepository.GetBySlug(slug, context.RequestAborted);
                if (tenant is not null)
                {
                    IMutableTenantContext tenantContext =
                        context.RequestServices.GetRequiredService<IMutableTenantContext>();
                    tenantContext.Set(tenant.Id, tenant.DbRole);
                }
            }

            await next(context);
        });

    /// <summary>
    ///     Extracts and validates the tenant slug from <paramref name="host" /> against
    ///     <paramref name="baseDomain" />, or <c>null</c> when the host is the apex, a reserved
    ///     subdomain, not a <c>&lt;slug&gt;.&lt;baseDomain&gt;</c> form, or the extracted label fails
    ///     DNS-label validation (docs/1-design.md "Naming safety").
    /// </summary>
    internal static string? ExtractTenantSlug(string host, string baseDomain, IReadOnlyCollection<string> reservedSubdomains)
    {
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(baseDomain))
        {
            return null;
        }

        var suffix = "." + baseDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            // Apex (naked base domain) or an unrelated host entirely: no tenant.
            return null;
        }

        var candidate = host[..^suffix.Length];

        // A multi-label remainder (e.g. "a.b.booqr.dk") is not a single-level tenant subdomain.
        if (candidate.Length == 0 || candidate.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var reserved in reservedSubdomains)
        {
            if (string.Equals(candidate, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        var lowerCandidate = candidate.ToLowerInvariant();
        return DnsLabelRegex().IsMatch(lowerCandidate) ? lowerCandidate : null;
    }

    // Slug validated as a DNS label per docs/1-design.md "Naming safety".
    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,30}[a-z0-9])?$")]
    private static partial Regex DnsLabelRegex();
}
