using System.Globalization;
using System.Security.Claims;
using Klinkby.Booqr.Application.Abstractions;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Api.Filters;

/// <summary>
///     Intercepts endpoints whose bound parameter implements
///     <see cref="IAuthenticatedRequest" />, injecting the authenticated user and the host-resolved
///     tenant id before the handler runs.
/// </summary>
/// <remarks>
///     Also enforces the claim-vs-host tenant guard (docs/1-design.md §5 "Missing/mismatched tenant
///     claim → 403"): when the caller is authenticated and its JWT carries a <c>tenant</c> claim, that
///     claim must equal the host-resolved <see cref="ITenantContext.TenantId" /> or the request is
///     rejected with 403. This runs post-authentication (endpoint filters execute after
///     <c>UseAuthorization</c> has populated <see cref="HttpContext.User" />), which is the earliest
///     point the claim is available. The DB remains the backstop (RLS) even if this check were ever
///     bypassed.
/// </remarks>
internal sealed class AuthenticatedRequestEndPointFilter : IEndpointFilter
{
    private const string TenantClaimType = "tenant";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;
        ClaimsPrincipal user = httpContext.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var tenantClaimValue = user.FindFirst(TenantClaimType)?.Value;
            if (tenantClaimValue is not null)
            {
                ITenantContext tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
                if (!int.TryParse(tenantClaimValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var claimTenantId)
                    || !tenantContext.HasTenant
                    || claimTenantId != tenantContext.TenantId)
                {
                    return ValueTask.FromResult<object?>(TypedResults.Problem(
                        "The authenticated tenant does not match the request host.",
                        null,
                        StatusCodes.Status403Forbidden,
                        "Tenant mismatch",
                        "https://www.booqr.dk/problems/tenant-mismatch"));
                }
            }
        }

        IList<object?> arguments = context.Arguments;
        // Iterate in reverse: request parameters tend to be at the end of the argument list.
        for (var i = arguments.Count - 1; i >= 0; i--)
        {
            if (arguments[i] is not IAuthenticatedRequest request)
            {
                continue;
            }

            request.SetUser(user);
            ITenantContext tenantContextForRequest =
                httpContext.RequestServices.GetRequiredService<ITenantContext>();
            if (tenantContextForRequest.HasTenant)
            {
                request.SetTenantId(tenantContextForRequest.TenantId);
            }

            break;
        }

        return next(context);
    }
}
