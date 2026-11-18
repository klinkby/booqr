using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Klinkby.Booqr.Core;
using Microsoft.AspNetCore.Mvc;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     HTTP-boundary binding/validation of the optional <c>Id</c> query array on <c>GET /api/users</c>.
///     Each request carries an invalid value so validation rejects it before the endpoint or database.
/// </summary>
public class UserCollectionValidationTests
{
    private const string Path = "/api/users";

    private static async Task<(HttpStatusCode, ValidationProblemDetails?)> Get(string query)
    {
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuth.CreateToken(1, UserRole.Admin));

        HttpResponseMessage response = await client.GetAsync(new Uri($"{Path}?{query}", UriKind.Relative));
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<ValidationProblemDetails>());
    }

    [Fact]
    public async Task GIVEN_NoId_WHEN_GetUsers_THEN_IdNotRequired()
    {
        // Act: an out-of-range Num forces a 400 after binding; a required Id would fail binding instead
        (HttpStatusCode status, ValidationProblemDetails? problem) = await Get("Num=1001");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors.Keys, k => k.Equals("Num", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(problem.Errors.Keys, k => k.Equals("Id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GIVEN_MoreThan100Id_WHEN_GetUsers_THEN_BadRequestNamesId()
    {
        // Arrange
        string query = string.Join('&', Enumerable.Range(1, 101).Select(id => $"Id={id}"));

        // Act
        (HttpStatusCode status, ValidationProblemDetails? problem) = await Get(query);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors.Keys, k => k.Equals("Id", StringComparison.OrdinalIgnoreCase));
    }
}
