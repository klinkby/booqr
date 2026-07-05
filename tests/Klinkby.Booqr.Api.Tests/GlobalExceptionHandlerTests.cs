using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;

namespace Klinkby.Booqr.Api.Tests;

public class GlobalExceptionHandlerTests
{
    private static readonly Uri ThrowUri = new("api/test-throw", UriKind.Relative);

    [Fact]
    public async Task GIVEN_UnhandledException_WHEN_RequestingEndpoint_THEN_ReturnsProblemJson()
    {
        await using WebApiFixture fixture = new(withThrowingEndpoint: true);
        HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync(ThrowUri);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.ProblemJson, response.Content.Headers.ContentType!.MediaType);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.GatewayTimeout, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task GIVEN_UnhandledExceptionAndHtmlAccept_WHEN_RequestingEndpoint_THEN_StillReturnsProblemJson()
    {
        await using WebApiFixture fixture = new(withThrowingEndpoint: true);
        HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, ThrowUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Text.Html));

        HttpResponseMessage response = await client.SendAsync(request);

        // The content type must be forced regardless of the Accept header (the regression guard
        // versus content-negotiated exception bodies).
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.ProblemJson, response.Content.Headers.ContentType!.MediaType);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.GatewayTimeout, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
