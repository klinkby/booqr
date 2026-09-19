using Microsoft.AspNetCore.Http.HttpResults;

namespace Klinkby.Booqr.Api.Filters;

/// <summary>
///     Single source of the tenant-related RFC 7807 problem responses, shared by the tenant
///     endpoint filters and the <c>GET /api/my-tenant</c> handler so the status codes, titles, and
///     problem-type URLs cannot drift between call sites.
/// </summary>
internal static class TenantProblems
{
    /// <summary>404 when the request host did not resolve to a known tenant.</summary>
    internal static ProblemHttpResult NotFound() =>
        TypedResults.Problem(
            "The request host did not resolve to a known tenant.",
            null,
            StatusCodes.Status404NotFound,
            "Tenant not found",
            "https://www.booqr.dk/problems/tenant-not-found");

    /// <summary>403 when the authenticated tenant claim does not match the host-resolved tenant.</summary>
    internal static ProblemHttpResult Mismatch() =>
        TypedResults.Problem(
            "The authenticated tenant does not match the request host.",
            null,
            StatusCodes.Status403Forbidden,
            "Tenant mismatch",
            "https://www.booqr.dk/problems/tenant-mismatch");
}
