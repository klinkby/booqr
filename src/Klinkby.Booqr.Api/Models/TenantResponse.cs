namespace Klinkby.Booqr.Api.Models;

/// <summary>
///     Public branding for <c>GET /api/tenant</c> (docs/1-design.md §3 "Frontend contract"). The
///     Core <see cref="Klinkby.Booqr.Core.Tenant" /> record has no logo/branding fields beyond
///     display name and slug; if the SPA needs more (e.g. a logo URL, theme colors), that is a
///     separate, out-of-scope addition to the <c>Tenant</c> registry shape.
/// </summary>
internal readonly record struct TenantResponse(string DisplayName, string Slug);
