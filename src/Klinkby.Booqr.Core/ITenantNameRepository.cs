namespace Klinkby.Booqr.Core;

/// <summary>
///     Provides the display names of live tenants for cross-tenant batch work (e.g. branding reminder mail).
/// </summary>
/// <remarks>Only usable inside an enabled <see cref="IBatchScope"/>; tenant roles have no grant on the registry.</remarks>
public interface ITenantNameRepository : IRepository
{
    /// <summary>
    ///     Retrieves the display names of all non-deleted tenants.
    /// </summary>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>A task whose result maps tenant id to display name.</returns>
    Task<IReadOnlyDictionary<int, string>> GetDisplayNames(CancellationToken cancellation = default);
}
