namespace Klinkby.Booqr.Application.Commands.Takeout;

internal static class TakeoutQueries
{
    /// <summary>Unbounded page (<c>LIMIT NULL</c>): takeout reads every active row of the tenant.</summary>
    internal static readonly PageQuery All = new(0, null);
}

/// <summary>
///     Export request. Each endpoint overwrites <see cref="Scope" /> with its fixed value, so a client-supplied
///     <c>?scope=</c> has no effect; the command also authorizes per scope:
///     <see cref="TakeoutScope.TenantWorkspace" /> requires an Admin (re-checked in the database);
///     <see cref="TakeoutScope.CustomerGdpr" /> exports the authenticated user's own data.
/// </summary>
public sealed record ExportTakeoutRequest(TakeoutScope Scope = TakeoutScope.CustomerGdpr) : AuthenticatedRequest;

/// <summary>
///     Import request (Admin only). Only <see cref="TakeoutScope.TenantWorkspace" /> documents are accepted.
/// </summary>
public sealed record ImportTakeoutRequest(
    [property: Required] TakeoutDocument Document,
    bool ValidateOnly = false) : AuthenticatedRequest;
