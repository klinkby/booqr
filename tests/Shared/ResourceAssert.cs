using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;

namespace Klinkby.Booqr.Tests.Shared;

/// <summary>
///     Link-compiled into the test projects that own localized resources. Guards the English (neutral) /
///     Danish (satellite) pair against drift: every key translated, same format placeholders.
/// </summary>
internal static partial class ResourceAssert
{
    internal static void DanishSatelliteMatchesNeutral(ResourceManager manager)
    {
        Dictionary<string, string> neutral = Load(manager, CultureInfo.InvariantCulture);
        Dictionary<string, string> danish = Load(manager, CultureInfo.GetCultureInfo("da"));

        Assert.NotEmpty(neutral);
        Assert.Equal(neutral.Keys.Order(StringComparer.Ordinal), danish.Keys.Order(StringComparer.Ordinal));
        Assert.All(neutral, pair =>
        {
            Assert.NotEqual(pair.Value, danish[pair.Key]);
            Assert.Equal(Placeholders(pair.Value), Placeholders(danish[pair.Key]));
        });
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
