using System.Collections;
using System.Globalization;
using System.Net;
using System.Resources;
using System.Text.Json;
using Klinkby.Booqr.Api.Properties;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     Problem <c>detail</c> text is localized from <c>Accept-Language</c>: English by default,
///     Danish on demand, with region-specific tags falling back to their parent language.
/// </summary>
public class ProblemLocalizationTests
{
    private const string ReservedHost = "www.booqr.dk"; // resolves no tenant without touching the registry

    [Theory]
    [InlineData(null, "en")]
    [InlineData("da", "da")]
    [InlineData("da-DK", "da")]
    [InlineData("fr-FR, da;q=0.5", "da")]
    [InlineData("fr", "en")]
    [InlineData("en-GB", "en")]
    public async Task GIVEN_AcceptLanguage_WHEN_ProblemReturned_THEN_DetailIsLocalized(string? acceptLanguage, string expected)
    {
        // Arrange
        await using WebApiFixture fixture = new(allowedHosts: ReservedHost, tenantRepository: new FakeTenantRepository(null));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/my-tenant", UriKind.Relative));
        request.Headers.Host = ReservedHost;
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }


        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            StringResources.ResourceManager.GetString(nameof(StringResources.TenantNotFound), CultureInfo.GetCultureInfo(expected)),
            body.RootElement.GetProperty("detail").GetString());
        Assert.Equal([expected], response.Content.Headers.ContentLanguage);
        Assert.Contains("Accept-Language", response.Headers.Vary);
    }

    [Fact]
    public void GIVEN_NeutralResources_WHEN_LoadingDanishSatellite_THEN_EveryKeyIsTranslated()
    {
        Dictionary<string, string> neutral = Load(CultureInfo.InvariantCulture);
        Dictionary<string, string> danish = Load(CultureInfo.GetCultureInfo("da"));

        Assert.NotEmpty(neutral);
        Assert.Equal(neutral.Keys.Order(StringComparer.Ordinal), danish.Keys.Order(StringComparer.Ordinal));
        Assert.All(neutral, pair => Assert.NotEqual(pair.Value, danish[pair.Key]));
    }

    [Fact]
    public async Task GIVEN_AcceptLanguage_WHEN_NonProblemResponse_THEN_NotLabelledWithLanguage()
    {
        // Arrange
        await using WebApiFixture fixture = new(allowedHosts: ReservedHost, tenantRepository: new FakeTenantRepository(null));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/health", UriKind.Relative));
        request.Headers.Host = ReservedHost;
        request.Headers.TryAddWithoutValidation("Accept-Language", "da");

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Empty(response.Content.Headers.ContentLanguage);
        Assert.DoesNotContain("Accept-Language", response.Headers.Vary);
    }

    private static Dictionary<string, string> Load(CultureInfo culture)
    {
        ResourceSet set = StringResources.ResourceManager.GetResourceSet(culture, true, false)
                          ?? throw new InvalidOperationException($"No resources for '{culture.Name}'");
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!, StringComparer.Ordinal);
    }
}
