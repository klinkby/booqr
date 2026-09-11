using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Infrastructure.Tests;

/// <summary>
///     Test double for <see cref="ITenantContext" /> used by integration tests to provide a
///     default tenant context without requiring the real tenant-resolution middleware (Phase 4).
///     Tests that need to exercise multi-tenant isolation can resolve their own
///     <see cref="NpgsqlConnection" /> via <see cref="ServiceProviderFixture.OpenTenantConnection" />.
/// </summary>
internal sealed class TestTenantContext : ITenantContext
{
    public TestTenantContext(int tenantId)
    {
        TenantId = tenantId;
        DbRole = $"t_{tenantId}";
        HasTenant = true;
    }

    public int TenantId { get; }

    public string DbRole { get; }

    public bool HasTenant { get; }
}
