using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Klinkby.Booqr.Application.Util;

/// <summary>Formats a localized <see cref="Properties.StringResources" /> template.</summary>
internal static class Localized
{
    /// <summary>
    ///     Fills <paramref name="format" /> (already resolved for the current UI culture) with invariant-formatted
    ///     arguments, so ids and counts render identically regardless of the caller's language.
    /// </summary>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'",
        Justification = "The template varies with the request's UI culture; a cached CompositeFormat would pin one language.")]
    internal static string Format(string format, params ReadOnlySpan<object?> args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
