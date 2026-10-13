using System.Globalization;

namespace Klinkby.Booqr.Application.Util;

internal static class MailTime
{
    // Requires tzdata in the runtime image (see Dockerfile); InvariantGlobalization does not affect it.
    internal static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    // InvariantGlobalization is on, so "dddd"/"MMM" would yield English names; spell out Danish ones.
    private static readonly string[] DayNames =
        ["søndag", "mandag", "tirsdag", "onsdag", "torsdag", "fredag", "lørdag"]; // indexed by DayOfWeek

    private static readonly string[] MonthAbbreviations =
        ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "aug", "sep", "okt", "nov", "dec"];

    /// <summary>Formats a UTC instant as Danish local time, e.g. <c>tirsdag 3. nov kl. 14:30</c>.</summary>
    internal static string FormatStart(DateTime utc)
    {
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Copenhagen);
        return string.Create(CultureInfo.InvariantCulture,
            $"{DayNames[(int)local.DayOfWeek]} {local.Day}. {MonthAbbreviations[local.Month - 1]} kl. {local:HH:mm}");
    }

    /// <summary>The Danish calendar date of a UTC instant.</summary>
    internal static DateOnly LocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Copenhagen));

    /// <summary>The UTC range [midnight, next midnight) of a Danish calendar date; 23-25 hours across DST changes.</summary>
    internal static (DateTime From, DateTime To) DayRangeUtc(DateOnly date) =>
        (ToUtc(date), ToUtc(date.AddDays(1)));

    private static DateTime ToUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Copenhagen);
}
