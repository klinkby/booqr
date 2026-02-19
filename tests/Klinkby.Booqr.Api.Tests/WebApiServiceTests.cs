using System.Net;
using System.Net.Mime;
using System.Text;

namespace Klinkby.Booqr.Api.Tests;

public class WebApiServiceTests
{
    [Fact]
    public async Task GIVEN_OpenApiRequest_THEN_Succeeds()
    {
        await using WebApiFixture fixture = new();
        HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("api/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task GIVEN_IncompleteLoginRequest_THEN_ValidationFails()
    {
        await using WebApiFixture fixture = new();
        HttpClient client = fixture.CreateClient();
        using StringContent emptyRequest = new(
            """
            {"username": "a", "password": ""}
            """,
            Encoding.UTF8, "application/json");

        HttpResponseMessage response =
            await client.PostAsync(new Uri("api/auth/login", UriKind.Relative), emptyRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.ProblemJson, response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task GIVEN_HealthRequest_THEN_RespondsWithStatus()
    {
        await using WebApiFixture fixture = new();
        HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/health", UriKind.Relative));

        // Health endpoint should return either OK (healthy) or ServiceUnavailable (unhealthy)
        // In test environment without database, it's expected to be unhealthy
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable, got {response.StatusCode}");
        Assert.Equal(MediaTypeNames.Text.Plain, response.Content.Headers.ContentType!.MediaType);
        
        string content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }
}
