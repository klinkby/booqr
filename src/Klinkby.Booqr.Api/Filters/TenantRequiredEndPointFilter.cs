using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Api.Filters;

/// <summary>
///     Rejects requests to tenant-scoped endpoints before the handler (and therefore before any
///     database access) when no tenant was resolved for the request (<see cref="ITenantContext.HasTenant" />
///     is <c>false</c>). Without this guard, such requests would reach the scoped tenant
///     <c>DbConnection</c>, which fails closed by throwing — surfacing as an unhandled 500 instead of
///     a clean 404.
/// </summary>
/// <remarks>
///     Applied to the <c>/api</c> route group in <c>Routing.cs</c>, alongside
///     <see cref="AuthenticatedRequestEndPointFilter" />. Endpoints that must remain reachable with no
///     tenant (health, <c>GET /api/my-tenant</c> itself, the OpenAPI document, auth login/refresh/logout)
///     opt out via <see cref="TenantOptionalAttribute" /> endpoint metadata — see <c>Routing.cs</c> for
///     where it's applied. Every other <c>/api</c> endpoint reads or writes tenant-scoped data (directly,
///     or transitively via row-level security), so tenant resolution is required by default.
/// </remarks>
internal sealed class TenantRequiredEndPointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;
        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<TenantOptionalAttribute>() is not null)
        {
            return next(context);
        }

        ITenantContext tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
        if (!tenantContext.HasTenant)
        {
            return ValueTask.FromResult<object?>(TenantProblems.NotFound());
        }

        return next(context);
    }
}

/// <summary>
///     Endpoint metadata marker opting an endpoint out of <see cref="TenantRequiredEndPointFilter" />'s
///     default tenant requirement (e.g. auth login/refresh/logout, the OpenAPI document).
/// </summary>
internal sealed class TenantOptionalAttribute;
