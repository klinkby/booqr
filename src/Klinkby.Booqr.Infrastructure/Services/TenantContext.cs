using System.Diagnostics.CodeAnalysis;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Infrastructure.Services;

/// <summary>
///     Scoped, mutable <see cref="ITenantContext" /> populated once per request by the
///     tenant-resolution middleware (Phase 4). Defaults to no tenant
///     (<see cref="ITenantContext.HasTenant" /> is <c>false</c>) so anonymous/reserved/apex hosts
///     and startup have a well-defined empty context rather than a missing DI registration.
/// </summary>
public interface IMutableTenantContext : ITenantContext
{
    /// <summary>
    ///     Assigns the resolved tenant to this scope. Idempotent per scope: the middleware calls it
    ///     at most once before any tenant-scoped data access.
    /// </summary>
    /// <param name="tenantId">The resolved tenant's positive integer id.</param>
    /// <param name="dbRole">The tenant's PostgreSQL login role (e.g. <c>t_42</c>).</param>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "'Set' reads clearly as the mutator for this scoped context; not a public library API surface.")]
    void Set(int tenantId, string dbRole);
}

/// <inheritdoc cref="IMutableTenantContext" />
internal sealed class TenantContext : IMutableTenantContext
{
    public int TenantId { get; private set; }

    public string DbRole { get; private set; } = string.Empty;

    public bool HasTenant { get; private set; }

    public void Set(int tenantId, string dbRole)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tenantId, 0);
        ArgumentException.ThrowIfNullOrEmpty(dbRole);

        TenantId = tenantId;
        DbRole = dbRole;
        HasTenant = true;
    }
}
