using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Klinkby.Booqr.Core;
using Microsoft.IdentityModel.Tokens;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     Covers Phase 4 tenant resolution: <c>GET /api/tenant</c> branding/404, and the claim-vs-host
///     403 guard in <see cref="Filters.AuthenticatedRequestEndPointFilter" />. Uses a fake
///     <see cref="ITenantRepository" /> (via <see cref="WebApiFixture" />) so these stay deterministic
///     without a live registry database.
/// </summary>
public class TenantEndpointsTests
{
    private const string JwtKey = "fa15a2b3982173649182736498127364192387648ad08alskdjcnlaskjdncbbdba";
    private const string JwtIssuer = "booqr";
    private const string JwtAudience = "https://www.booqr.dk";
    private const string AliceHost = "alice.booqr.dk";
    private const int AliceTenantId = 42;
    private const string AliceDisplayName = "Alice's Salon";

    private static readonly Tenant AliceTenant = new(AliceTenantId, "alice", "t_42", AliceDisplayName);

    private sealed class FakeTenantRepository(Tenant? tenant) : ITenantRepository
    {
        public Task<Tenant?> GetBySlug(string slug, CancellationToken cancellation = default) =>
            Task.FromResult(string.Equals(slug, tenant?.Slug, StringComparison.Ordinal) ? tenant : null);
    }

    private static string CreateToken(int userId, string? role, int? tenantId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        List<Claim> claims = [new(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture))];
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (tenantId is not null)
        {
            claims.Add(new Claim("tenant", tenantId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            Expires = DateTime.UtcNow.AddHours(1),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            Subject = new ClaimsIdentity(claims)
        };
        var handler = new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    [Fact]
    public async Task GIVEN_KnownTenantHost_WHEN_GetTenant_THEN_ReturnsBranding()
    {
        await using WebApiFixture fixture = new(allowedHosts: AliceHost, tenantRepository: new FakeTenantRepository(AliceTenant));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/tenant", UriKind.Relative));
        request.Headers.Host = AliceHost;

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(AliceDisplayName, body.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("alice", body.RootElement.GetProperty("slug").GetString());
    }

    [Theory]
    [InlineData("www.booqr.dk")] // reserved subdomain
    [InlineData("booqr.dk")] // apex
    [InlineData("ghost.booqr.dk")] // unknown slug
    public async Task GIVEN_NoResolvableTenantHost_WHEN_GetTenant_THEN_NotFound(string host)
    {
        await using WebApiFixture fixture = new(allowedHosts: host, tenantRepository: new FakeTenantRepository(AliceTenant));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/tenant", UriKind.Relative));
        request.Headers.Host = host;

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "https://www.booqr.dk/problems/tenant-not-found",
            body.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GIVEN_MatchingTenantClaim_WHEN_AccessingTenantEndpoint_THEN_NotRejectedByClaimGuard()
    {
        await using WebApiFixture fixture = new(allowedHosts: AliceHost, tenantRepository: new FakeTenantRepository(AliceTenant));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/users/2", UriKind.Relative));
        request.Headers.Host = AliceHost;
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(2, "Customer", AliceTenantId));

        HttpResponseMessage response = await client.SendAsync(request);

        // Claim matches the host-resolved tenant, so the request proceeds past the guard; it still
        // fails downstream (no live tenant DB in this fixture), but must not be the 403 the guard
        // itself would produce for a mismatch.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GIVEN_MismatchedTenantClaim_WHEN_AccessingTenantEndpoint_THEN_Forbidden()
    {
        await using WebApiFixture fixture = new(allowedHosts: AliceHost, tenantRepository: new FakeTenantRepository(AliceTenant));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/users/2", UriKind.Relative));
        request.Headers.Host = AliceHost;
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(2, "Customer", AliceTenantId + 1));

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "https://www.booqr.dk/problems/tenant-mismatch",
            body.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GIVEN_NoTenantResolved_WHEN_AccessingTenantRequiredEndpoint_THEN_NotFoundBeforeHandler()
    {
        await using WebApiFixture fixture = new(allowedHosts: "www.booqr.dk", tenantRepository: new FakeTenantRepository(AliceTenant));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/employees", UriKind.Relative));
        request.Headers.Host = "www.booqr.dk"; // reserved: no tenant resolved

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "https://www.booqr.dk/problems/tenant-not-found",
            body.RootElement.GetProperty("type").GetString());
    }
}
