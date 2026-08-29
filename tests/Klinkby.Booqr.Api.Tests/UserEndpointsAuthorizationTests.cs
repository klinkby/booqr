using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Klinkby.Booqr.Core;
using Microsoft.IdentityModel.Tokens;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     HTTP-boundary guardrails for the user lookup endpoints. These assert the route
///     authorization policy that keeps anonymous callers (and callers without a recognized
///     role) out of the user resource entirely. They deliberately hit only paths that are
///     decided by the auth middleware BEFORE the endpoint/database, so no database is needed.
/// </summary>
public class UserEndpointsAuthorizationTests
{
    // Matches WebApiFixture's Application:Jwt configuration.
    private const string JwtKey = "fa15a2b3982173649182736498127364192387648ad08alskdjcnlaskjdncbbdba";
    private const string JwtIssuer = "booqr";
    private const string JwtAudience = "https://www.booqr.dk";

    private static string CreateToken(int userId, string? role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        List<Claim> claims = [new(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture))];
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
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
        SetBearer(client, CreateToken(2, role: null));

        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
