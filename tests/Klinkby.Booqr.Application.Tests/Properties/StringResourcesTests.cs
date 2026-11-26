using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;

namespace Klinkby.Booqr.Application.Tests.Properties;

/// <summary>
///     Guards the English (neutral) / Danish (satellite) problem-detail resources against drift:
///     every key must be translated, and translations must keep the same format placeholders.
/// </summary>
public partial class StringResourcesTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da");

    [Fact]
    public void GIVEN_NeutralResources_WHEN_LoadingDanishSatellite_THEN_EveryKeyIsTranslatedWithSamePlaceholders()
    {
        Dictionary<string, string> neutral = Load(StringResources.ResourceManager, CultureInfo.InvariantCulture);
        Dictionary<string, string> danish = Load(StringResources.ResourceManager, Danish);

        Assert.NotEmpty(neutral);
        Assert.Equal(neutral.Keys.Order(StringComparer.Ordinal), danish.Keys.Order(StringComparer.Ordinal));
        Assert.All(neutral, pair =>
        {
            Assert.NotEqual(pair.Value, danish[pair.Key]);
            Assert.Equal(Placeholders(pair.Value), Placeholders(danish[pair.Key]));
        });
    }

    [Fact]
    public void GIVEN_DanishUiCulture_WHEN_ReadingDetail_THEN_DanishTextIsReturned()
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = Danish;
            var danish = StringResources.VacancyAlreadyBooked;
            CultureInfo.CurrentUICulture = English;
            var english = StringResources.VacancyAlreadyBooked;

            Assert.Equal(StringResources.ResourceManager.GetString(nameof(StringResources.VacancyAlreadyBooked), Danish), danish);
            Assert.NotEqual(english, danish);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void GIVEN_AnyUiCulture_WHEN_ReadingMailSubject_THEN_SubjectIsCultureIndependent()
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = English;
            var english = MailResources.NewBooking;
            CultureInfo.CurrentUICulture = Danish;
            var danish = MailResources.NewBooking;

            // Mail bodies are Danish-only templates, so subjects must not follow the request culture.
            Assert.Equal(english, danish);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    private static Dictionary<string, string> Load(ResourceManager manager, CultureInfo culture)
    {
        ResourceSet set = manager.GetResourceSet(culture, true, false)
                          ?? throw new InvalidOperationException($"No resources for '{culture.Name}'");
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!, StringComparer.Ordinal);
    }

    private static string[] Placeholders(string format) =>
        PlaceholderRegex().Matches(format).Select(m => m.Value).Order(StringComparer.Ordinal).ToArray();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderRegex();
}
