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

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

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
    public async Task GIVEN_HealthRequest_THEN_Succeeds()
    {
        await using WebApiFixture fixture = new();
        HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MediaTypeNames.Text.Plain, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
