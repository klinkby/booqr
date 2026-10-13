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
    /// <param name="tenant">The resolved tenant registry record.</param>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "'Set' reads clearly as the mutator for this scoped context; not a public library API surface.")]
    void Set(Tenant tenant);
}

/// <inheritdoc cref="IMutableTenantContext" />
internal sealed class TenantContext : IMutableTenantContext
{
    public int TenantId { get; private set; }

    public string DbRole { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool HasTenant { get; private set; }

    public void Set(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tenant.Id, 0);
        ArgumentException.ThrowIfNullOrEmpty(tenant.DbRole);
        ArgumentException.ThrowIfNullOrEmpty(tenant.Slug);

        TenantId = tenant.Id;
        DbRole = tenant.DbRole;
        Slug = tenant.Slug;
        DisplayName = tenant.DisplayName;
        HasTenant = true;
    }
}
