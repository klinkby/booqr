namespace Klinkby.Booqr.Core;

/// <summary>
///     Opt-in marker for cross-tenant background/scheduled work (reminder mail, refresh-token
///     flushing, the <c>ActivityBackgroundService</c> consumer) that must run as the
///     <c>booqr_batch</c> (<c>BYPASSRLS</c>) PostgreSQL role instead of a per-tenant connection.
/// </summary>
/// <remarks>
///     Scoped per background-service iteration's <c>IServiceScope</c>. A background service calls
///     <see cref="Enable" /> immediately after creating its scope, before resolving any repository.
///     Infrastructure's scoped connection resolution reads <see cref="IsEnabled" />: when
///     <c>true</c>, the scope's <c>DbConnection</c> is built from the <c>booqr_batch</c> data source
///     (cross-tenant, <c>BYPASSRLS</c>) instead of requiring <see cref="ITenantContext.HasTenant" />.
///     Never enable this for a request-handling scope - it deliberately bypasses row-level security.
/// </remarks>
public interface IBatchScope
{
    /// <summary>
    ///     Gets a value indicating whether this scope's database access should use the
    ///     <c>booqr_batch</c> (<c>BYPASSRLS</c>) connection.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Marks this scope as cross-tenant batch work. Idempotent; call once per scope before
    ///     resolving any repository.
    /// </summary>
    void Enable();
}
