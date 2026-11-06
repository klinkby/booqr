namespace Klinkby.Booqr.Application.Commands.Takeout;

/// <summary>
///     Export request. <see cref="Scope" /> is set by the endpoint (never bound from the client):
///     <see cref="TakeoutScope.TenantWorkspace" /> requires Admin; <see cref="TakeoutScope.CustomerGdpr" />
///     exports the authenticated user's own data.
/// </summary>
public sealed record ExportTakeoutRequest(TakeoutScope Scope = TakeoutScope.CustomerGdpr) : AuthenticatedRequest;

/// <summary>
///     Import request (Admin only). Only <see cref="TakeoutScope.TenantWorkspace" /> documents are accepted.
/// </summary>
public sealed record ImportTakeoutRequest(
    [property: Required] TakeoutDocument Document,
    bool ValidateOnly = false) : AuthenticatedRequest;
