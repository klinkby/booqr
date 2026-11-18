using System.Net;
using System.Net.Http.Headers;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     HTTP-boundary guardrails for the admin-only activity (audit log) endpoint. Only paths decided
///     by the auth middleware or request validation (before the command/database) are exercised.
/// </summary>
public class ActivityEndpointsAuthorizationTests
{
    private const string Path = "/api/activities";

    [Fact]
    public async Task GIVEN_Anonymous_WHEN_ListingActivities_THEN_Unauthorized()
    {
        // Arrange
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri(Path, UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static TheoryData<string> NonAdminRoles => [UserRole.Customer, UserRole.Employee];

    [Theory]
    [MemberData(nameof(NonAdminRoles))]
    public async Task GIVEN_NonAdminToken_WHEN_ListingActivities_THEN_Forbidden(string role)
    {
        // Arrange
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuth.CreateToken(2, role));

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri(Path, UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GIVEN_AdminToken_WHEN_NumOutOfRange_THEN_BadRequest()
    {
        // Arrange
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuth.CreateToken(1, UserRole.Admin));

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri($"{Path}?Num=1001", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
