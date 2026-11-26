using System.Globalization;
using Klinkby.Booqr.Tests.Shared;

namespace Klinkby.Booqr.Application.Tests.Properties;

/// <summary>Problem text follows the UI culture; mail subjects do not.</summary>
public class StringResourcesTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da");

    [Fact]
    public void GIVEN_NeutralResources_WHEN_LoadingDanishSatellite_THEN_EveryKeyIsTranslatedWithSamePlaceholders() =>
        ResourceAssert.DanishSatelliteMatchesNeutral(StringResources.ResourceManager);

    [Fact]
    public void GIVEN_DanishUiCulture_WHEN_ReadingDetail_THEN_DanishTextIsReturned()
    {
        var danish = WithUiCulture(Danish, static () => StringResources.VacancyAlreadyBooked);
        var english = WithUiCulture(English, static () => StringResources.VacancyAlreadyBooked);

        Assert.Equal(StringResources.ResourceManager.GetString(nameof(StringResources.VacancyAlreadyBooked), Danish), danish);
        Assert.NotEqual(english, danish);
    }

    [Fact]
    public void GIVEN_AnyUiCulture_WHEN_ReadingMailSubject_THEN_SubjectIsCultureIndependent()
    {
        var english = WithUiCulture(English, static () => MailResources.NewBooking);
        var danish = WithUiCulture(Danish, static () => MailResources.NewBooking);

        // Mail bodies are Danish-only templates, so subjects must not follow the request culture.
        Assert.Equal(english, danish);
    }

    private static T WithUiCulture<T>(CultureInfo culture, Func<T> read)
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
