using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Klinkby.Booqr.Application.Util;

/// <summary>
///     Serializes booking events as an RFC 5545 iCalendar (VCALENDAR) document.
/// </summary>
internal static class CalendarSerializer
{
    private const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";
    private const int MaxLineOctets = 75;
    private const int MaxContinuationOctets = 74; // plus the leading space = 75
    private const string Crlf = "\r\n";

    /// <summary>
    ///     Streams the events as a VCALENDAR document, one content line at a time (folded and CRLF-terminated),
    ///     so the document is never buffered whole; the caller writes the lines out as UTF-8.
    /// </summary>
    /// <param name="calendarName">Calendar display name, emitted as X-WR-CALNAME.</param>
    /// <param name="uidHost">Host part of each event UID, i.e. <c>booking-{id}@{uidHost}</c>.</param>
    /// <param name="stamp">DTSTAMP for the whole document (UTC).</param>
    /// <param name="events">The events to publish; may be empty. Enumerated once, as lines are produced.</param>
    /// <param name="cancellation">Cancels the enumeration.</param>
    internal static async IAsyncEnumerable<string> Lines(string calendarName, string uidHost, DateTime stamp,
        IAsyncEnumerable<CalendarFeedEvent> events, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        yield return Line("BEGIN:VCALENDAR");
        yield return Line("VERSION:2.0");
        yield return Line("PRODID:-//Klinkby//Booqr//EN");
        yield return Line("CALSCALE:GREGORIAN");
        yield return Line("METHOD:PUBLISH");
        yield return Line("X-WR-CALNAME:" + EscapeText(calendarName));

        string dtStamp = FormatUtc(stamp);
        string host = EscapeText(uidHost);
        await foreach (CalendarFeedEvent e in events.WithCancellation(cancellation))
        {
            yield return Line("BEGIN:VEVENT");
            yield return Line("UID:booking-" + e.Id.ToString(CultureInfo.InvariantCulture) + "@" + host);
            yield return Line("DTSTAMP:" + dtStamp);
            yield return Line("DTSTART:" + FormatUtc(e.StartTime));
            yield return Line("DTEND:" + FormatUtc(e.EndTime));
            yield return Line("LAST-MODIFIED:" + FormatUtc(e.Modified));
            yield return Line("SUMMARY:" + EscapeText(e.Service));
            yield return Line("LOCATION:" + EscapeText(FormatLocation(e)));
            yield return Line("END:VEVENT");
        }

        yield return Line("END:VCALENDAR");
    }

    /// <summary>Joins the location name and non-blank address parts; zip and city share one part.</summary>
    private static string FormatLocation(CalendarFeedEvent e) =>
        JoinNonBlank(", ", e.Location, e.Address1, e.Address2, JoinNonBlank(" ", e.Zip, e.City));

    private static string JoinNonBlank(string separator, params string?[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    /// <summary>Formats as UTC; <see cref="DateTimeKind.Unspecified" /> values (as read from the DB) are UTC.</summary>
    private static string FormatUtc(DateTime value) =>
        (value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value)
        .ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>TEXT value escaping per RFC 5545 section 3.3.11.</summary>
    private static string EscapeText(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);

    /// <summary>A content line, folded at 75 octets and terminated with CRLF.</summary>
    private static string Line(string line) => Fold(line) + Crlf;

    /// <summary>
    ///     Folds a line longer than 75 octets: CRLF plus one space before each continuation of at most 74 octets,
    ///     never splitting a UTF-8 sequence or surrogate pair. Short lines (the common case) are returned as is.
    /// </summary>
    private static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= MaxLineOctets)
        {
            return line;
        }

        var sb = new StringBuilder(line.Length + line.Length / MaxContinuationOctets * 3 + 3);
        int limit = MaxLineOctets;
        int used = 0;
        foreach (Rune rune in line.EnumerateRunes())
        {
            int octets = rune.Utf8SequenceLength;
            if (used + octets > limit)
            {
                sb.Append(Crlf).Append(' ');
                used = 0;
                limit = MaxContinuationOctets;
            }

            sb.Append(rune);
            used += octets;
        }

        return sb.ToString();
    }
}
