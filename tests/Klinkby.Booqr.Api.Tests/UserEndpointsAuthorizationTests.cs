using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     HTTP-boundary guardrails for the user lookup endpoints. These assert the route
///     authorization policy that keeps anonymous callers (and callers without a recognized
///     role) out of the user resource entirely. They deliberately hit only paths that are
///     decided by the auth middleware BEFORE the endpoint/database, so no database is needed.
/// </summary>
public class UserEndpointsAuthorizationTests
{
    private static void SetBearer(HttpClient client, string? token)
    {
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public static TheoryData<string> UserPaths =>
        ["/api/users/2", "/api/users?Role=Employee&Start=0&Num=100"];

    [Theory]
    [MemberData(nameof(UserPaths))]
    public async Task GIVEN_Anonymous_WHEN_LookingUpUsers_THEN_Unauthorized(string path)
    {
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(UserPaths))]
    public async Task GIVEN_TokenWithoutRole_WHEN_LookingUpUsers_THEN_Forbidden(string path)
    {
        // A validly signed token that carries no recognized role must not satisfy the
        // Customer policy, so it is rejected before reaching the endpoint or database.
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();
        SetBearer(client, TestAuth.CreateToken(2, role: null));

        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public static TheoryData<string> RoleChangePaths => ["/api/users/2/role"];

    [Theory]
    [MemberData(nameof(RoleChangePaths))]
    public async Task GIVEN_Anonymous_WHEN_ChangingUserRole_THEN_Unauthorized(string path)
    {
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();
        using StringContent body = new("""{"role":"Employee"}""", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PutAsync(new Uri(path, UriKind.Relative), body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static TheoryData<string, string> NonAdminRoleChangeCases =>
        [
            ("/api/users/2/role", "Customer"),
            ("/api/users/2/role", "Employee")
        ];

    [Theory]
    [MemberData(nameof(NonAdminRoleChangeCases))]
    public async Task GIVEN_NonAdminToken_WHEN_ChangingUserRole_THEN_Forbidden(string path, string role)
    {
        // Role changes are admin-only; Customer and Employee tokens must be rejected before
        // the endpoint or database is reached.
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();
        SetBearer(client, TestAuth.CreateToken(2, role));
        using StringContent body = new("""{"role":"Admin"}""", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PutAsync(new Uri(path, UriKind.Relative), body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RoleChangePaths))]
    public async Task GIVEN_AdminToken_WHEN_ChangingToUnknownRole_THEN_BadRequest(string path)
    {
        // Validation rejects anything outside the Customer/Employee/Admin allow-list before
        // the command (and its database access) runs.
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();
        SetBearer(client, TestAuth.CreateToken(1, UserRole.Admin));
        using StringContent body = new("""{"role":"Root"}""", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PutAsync(new Uri(path, UriKind.Relative), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("Role", problem, StringComparison.Ordinal);
    }
}
