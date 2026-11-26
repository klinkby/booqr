using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Klinkby.Booqr.Api.Properties;
using Klinkby.Booqr.Tests.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     Problem <c>title</c>/<c>detail</c> text is localized from <c>Accept-Language</c>: English by default,
///     Danish on demand, with region-specific tags falling back to their parent language. Only localized
///     problems are labelled with <c>Vary</c>/<c>Content-Language</c>.
/// </summary>
public class ProblemLocalizationTests
{
    private const string ReservedHost = "www.booqr.dk"; // resolves no tenant without touching the registry
    private static readonly Uri MyTenantUri = new("/api/my-tenant", UriKind.Relative);
    private static readonly Uri ThrowUri = new("/api/test-throw", UriKind.Relative);

    [Theory]
    [InlineData(null, "en")]
    [InlineData("da", "da")]
    [InlineData("da-DK", "da")]
    [InlineData("fr-FR, da;q=0.5", "da")]
    [InlineData("fr", "en")]
    [InlineData("en-GB", "en")]
    public async Task GIVEN_AcceptLanguage_WHEN_ProblemReturned_THEN_TitleAndDetailAreLocalized(string? acceptLanguage, string expected)
    {
        // Arrange
        await using WebApiFixture fixture = TenantlessFixture();
        using HttpRequestMessage request = Request(HttpMethod.Get, MyTenantUri, acceptLanguage);

        // Act
        HttpResponseMessage response = await fixture.CreateClient().SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(Expected(nameof(StringResources.TenantNotFound), expected), problem.Detail);
        Assert.Equal(Expected(nameof(StringResources.TenantNotFoundTitle), expected), problem.Title);
        AssertLabelled(response, expected);
    }

    [Theory]
    [InlineData("da")]
    [InlineData("en")]
    public async Task GIVEN_AcceptLanguage_WHEN_UnhandledException_THEN_TitleIsLocalized(string language)
    {
        // Arrange
        await using WebApiFixture fixture = new(withThrowingEndpoint: true);
        using HttpRequestMessage request = Request(HttpMethod.Get, ThrowUri, language, host: null);

        // Act
        HttpResponseMessage response = await fixture.CreateClient().SendAsync(request);

        // Assert (the fixture throws TimeoutException -> 504)
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Expected(nameof(StringResources.Status504Title), language), problem?.Title);
        AssertLabelled(response, language);
    }

    [Fact]
    public async Task GIVEN_DanishAcceptLanguageAndHtmlAccept_WHEN_UnhandledException_THEN_FallbackIsLabelled()
    {
        // Arrange: Accept: text/html makes the problem writer decline, exercising the forced-JSON fallback.
        await using WebApiFixture fixture = new(withThrowingEndpoint: true);
        using HttpRequestMessage request = Request(HttpMethod.Get, ThrowUri, "da", host: null);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        // Act
        HttpResponseMessage response = await fixture.CreateClient().SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Expected(nameof(StringResources.Status504Title), "da"), problem?.Title);
        Assert.True(problem?.Extensions.ContainsKey("traceId"));
        AssertLabelled(response, "da");
    }

    [Fact]
    public async Task GIVEN_DanishAcceptLanguage_WHEN_FrameworkValidationProblem_THEN_NotLabelledDanish()
    {
        // Arrange: the framework's validation 400 (title, errors) is not localized.
        await using WebApiFixture fixture = new();
        using HttpRequestMessage request = Request(HttpMethod.Post, new Uri("/api/users", UriKind.Relative), "da", host: null);
        request.Content = new StringContent("""{"email":"user@example.com"}""", Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await fixture.CreateClient().SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentLanguage);
    }

    [Fact]
    public async Task GIVEN_AcceptLanguage_WHEN_NonProblemResponse_THEN_NotLabelledWithLanguage()
    {
        // Arrange
        await using WebApiFixture fixture = TenantlessFixture();
        using HttpRequestMessage request = Request(HttpMethod.Get, new Uri("/api/health", UriKind.Relative), "da");

        // Act
        HttpResponseMessage response = await fixture.CreateClient().SendAsync(request);

        // Assert
        Assert.Empty(response.Content.Headers.ContentLanguage);
        Assert.DoesNotContain("Accept-Language", response.Headers.Vary);
    }

    [Fact]
    public void GIVEN_NeutralResources_WHEN_LoadingDanishSatellite_THEN_EveryKeyIsTranslatedWithSamePlaceholders() =>
        ResourceAssert.DanishSatelliteMatchesNeutral(StringResources.ResourceManager);

    private static WebApiFixture TenantlessFixture() =>
        new(allowedHosts: ReservedHost, tenantRepository: new FakeTenantRepository(null));

    private static HttpRequestMessage Request(HttpMethod method, Uri uri, string? acceptLanguage, string? host = ReservedHost)
    {
        HttpRequestMessage request = new(method, uri);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        return request;
    }

    private static string? Expected(string key, string language) =>
        StringResources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(language));

    private static void AssertLabelled(HttpResponseMessage response, string language)
    {
        Assert.Equal([language], response.Content.Headers.ContentLanguage);
        Assert.Contains("Accept-Language", response.Headers.Vary);
    }
}
