using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     HTTP-boundary tests for the takeout endpoints. Only paths decided before the database is reached
///     (anonymous 401, role 403, the static JSON Schema, and tenant-required 404) are asserted; the
///     export/import happy paths need a live tenant database and are covered elsewhere.
/// </summary>
public class TakeoutEndpointTests
{
    private const string ReservedHost = "www.booqr.dk"; // resolves no tenant

    private static HttpRequestMessage CreateRequest(string method, string path, string host)
    {
        HttpRequestMessage request = new(new HttpMethod(method), new Uri(path, UriKind.Relative));
        request.Headers.Host = host;
        if (method == HttpMethod.Post.Method)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        return request;
    }

    public static TheoryData<string, string> TakeoutRoutes =>
        [
            ("GET", "/api/takeout"),
            ("GET", "/api/takeout/me"),
            ("POST", "/api/takeout")
        ];

    public static TheoryData<string, string, string> NonAdminTenantRoutes =>
        [
            ("GET", "/api/takeout", UserRole.Customer),
            ("GET", "/api/takeout", UserRole.Employee),
            ("POST", "/api/takeout", UserRole.Customer),
            ("POST", "/api/takeout", UserRole.Employee)
        ];

    [Fact]
    public async Task GIVEN_AnyHost_WHEN_GettingTakeoutSchema_THEN_ReturnsJsonSchema()
    {
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/takeout-v1.schema.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/schema+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("$id", out _));
    }

    [Theory]
    [MemberData(nameof(TakeoutRoutes))]
    public async Task GIVEN_Anonymous_WHEN_AccessingTakeout_THEN_Unauthorized(string method, string path)
    {
        await using WebApiFixture fixture = new(allowedHosts: ReservedHost, tenantRepository: new FakeTenantRepository(null));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = CreateRequest(method, path, ReservedHost);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(NonAdminTenantRoutes))]
    public async Task GIVEN_NonAdminToken_WHEN_AccessingTenantTakeout_THEN_Forbidden(string method, string path, string role)
    {
        // Tenant workspace export/import is Admin-only; the policy rejects these tokens before the
        // endpoint or database is reached.
        await using WebApiFixture fixture = new(allowedHosts: ReservedHost, tenantRepository: new FakeTenantRepository(null));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = CreateRequest(method, path, ReservedHost);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestAuth.CreateToken(2, role));

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GIVEN_CustomerToken_WHEN_ExportingMyTakeout_THEN_NotForbidden()
    {
        // Customer policy covers every role, so the authorization gate lets it through; with no tenant
        // resolved the tenant-required filter then answers 404 before the command touches the database.
        await using WebApiFixture fixture = new(allowedHosts: ReservedHost, tenantRepository: new FakeTenantRepository(null));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = CreateRequest("GET", "/api/takeout/me", ReservedHost);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestAuth.CreateToken(2, UserRole.Customer));

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GIVEN_AdminTokenWithoutTenant_WHEN_ExportingTenantTakeout_THEN_NotFoundBeforeHandler()
    {
        await using WebApiFixture fixture = new(allowedHosts: ReservedHost, tenantRepository: new FakeTenantRepository(null));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = CreateRequest("GET", "/api/takeout", ReservedHost);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestAuth.CreateToken(1, UserRole.Admin));

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "https://www.booqr.dk/problems/tenant-not-found",
            body.RootElement.GetProperty("type").GetString());
    }
}
