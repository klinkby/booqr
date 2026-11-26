using Klinkby.Booqr.Api.Properties;
using Klinkby.Booqr.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Klinkby.Booqr.Api.Filters;

/// <summary>
///     Single source of the tenant-related RFC 7807 problem responses, shared by the tenant
///     endpoint filters and the <c>GET /api/my-tenant</c> handler so the status codes, titles, and
///     problem-type URLs cannot drift between call sites. Detail and title are localized
///     (<c>Properties/StringResources</c>).
/// </summary>
internal static class TenantProblems
{
    /// <summary>404 when the request host did not resolve to a known tenant.</summary>
    internal static ProblemHttpResult NotFound() =>
        new Problem(Problem.Prefix + "tenant-not-found", StringResources.TenantNotFoundTitle,
            StatusCodes.Status404NotFound, StringResources.TenantNotFound).ToProblemHttpResult();

    /// <summary>403 when the authenticated tenant claim does not match the host-resolved tenant.</summary>
    internal static ProblemHttpResult Mismatch() =>
        new Problem(Problem.Prefix + "tenant-mismatch", StringResources.TenantMismatchTitle,
            StatusCodes.Status403Forbidden, StringResources.TenantMismatch).ToProblemHttpResult();
}
