// Tenant repository tests land in Phase 2b (registry data source and resolution implementation).
//
// Scaffolding below reflects the final Tenant record shape from Phase 1.
// When implementing TenantRepository:
//   - Query the public.tenants registry table using the booqr_registry role.
//   - Implement GetBySlug(slug) -> Task<Tenant?> for host resolution.
//   - Do NOT add GetAll/Update/Delete unless required by a later phase.
//
// using Klinkby.Booqr.Core;
// using Microsoft.Extensions.DependencyInjection;
//
// namespace Klinkby.Booqr.Infrastructure.Tests;
//
// public sealed class TenantsRepositoryTests : PostgreSqlFixture
// {
//     [Fact]
//     public async Task GIVEN_ValidSlug_WHEN_GetBySlug_THEN_ReturnsMatchingTenant()
//     {
//         var sut = Services.GetRequiredService<ITenantRepository>();
//         var tenant = await sut.GetBySlug("alice", CancellationToken.None);
//         Assert.NotNull(tenant);
//         Assert.Equal("alice", tenant.Slug);
//         Assert.True(tenant.DbRole.StartsWith("t_"));
//     }
//
//     [Fact]
//     public async Task GIVEN_UnknownSlug_WHEN_GetBySlug_THEN_ReturnsNull()
//     {
//         var sut = Services.GetRequiredService<ITenantRepository>();
//         var tenant = await sut.GetBySlug("nonexistent", CancellationToken.None);
//         Assert.Null(tenant);
//     }
// }
