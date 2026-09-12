namespace Klinkby.Booqr.Core;

/// <summary>
///     Provides tenant context information resolved from the incoming HTTP request.
/// </summary>
/// <remarks>
///     The tenant context is populated during request processing by the tenant-resolution middleware.
///     Reserved and apex hosts may resolve to no tenant, in which case <see cref="HasTenant"/> is <c>false</c>.
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    ///     Gets the identifier of the resolved tenant, or 0 if no tenant was resolved.
    /// </summary>
    /// <value>A positive integer tenant identifier, or 0 when <see cref="HasTenant"/> is <c>false</c>.</value>
    int TenantId { get; }

    /// <summary>
    ///     Gets the PostgreSQL login role name for the tenant (e.g., "t_42").
    /// </summary>
    /// <remarks>
    ///     This role is used to establish the database connection with row-level security (RLS) enforcement.
    ///     The role name is derived from the tenant identifier and is unforgeable.
    /// </remarks>
    /// <value>The role name, or an empty string when <see cref="HasTenant"/> is <c>false</c>.</value>
    string DbRole { get; }

    /// <summary>
    ///     Gets the public slug of the resolved tenant (the <c>&lt;slug&gt;</c> in
    ///     <c>&lt;slug&gt;.booqr.dk</c>), or an empty string when no tenant was resolved.
    /// </summary>
    /// <value>The tenant slug, or an empty string when <see cref="HasTenant"/> is <c>false</c>.</value>
    string Slug { get; }

    /// <summary>
    ///     Gets a value indicating whether a tenant was successfully resolved from the request host.
    /// </summary>
    /// <remarks>
    ///     Returns <c>false</c> for reserved hosts (e.g., "www", "api", naked "booqr.dk") and marketing routes.
    ///     When <c>false</c>, <see cref="TenantId"/> is 0 and <see cref="DbRole"/> is empty.
    /// </remarks>
    /// <value><c>true</c> if a tenant was resolved; otherwise <c>false</c>.</value>
    bool HasTenant { get; }
}
