using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Klinkby.Booqr.Core;
using Microsoft.IdentityModel.Tokens;

namespace Klinkby.Booqr.Api.Tests;

internal static class TestAuth
{
    // Matches WebApiFixture's Application:Jwt configuration.
    private const string JwtKey = "fa15a2b3982173649182736498127364192387648ad08alskdjcnlaskjdncbbdba";
    private const string JwtIssuer = "booqr";
    private const string JwtAudience = "https://www.booqr.dk";

    public static string CreateToken(int userId, string? role = null, int? tenantId = null)
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
}

internal sealed class FakeTenantRepository(Tenant? tenant) : ITenantRepository
{
    public Task<Tenant?> GetBySlug(string slug, CancellationToken cancellation = default) =>
        Task.FromResult(string.Equals(slug, tenant?.Slug, StringComparison.Ordinal) ? tenant : null);
}
