using System.Text;
using Klinkby.Booqr.Application.Util;

namespace Klinkby.Booqr.Application.Tests.Util;

public class CalendarSerializerTests
{
    private const string Host = "booqr.example";
    private static readonly DateTime Stamp = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private static async Task<string> Serialize(string calendarName, string host, DateTime stamp,
        IEnumerable<CalendarFeedEvent> events) =>
        string.Concat(await CalendarSerializer.Lines(calendarName, host, stamp, events.ToAsyncEnumerable())
            .ToListAsync());

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NoEvents_WHEN_Serialize_THEN_ValidEmptyCalendar(DateTime t0)
    {
        string output = await Serialize("Bookings", Host, t0, []);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", output, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", output, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN:VEVENT", output, StringComparison.Ordinal);
        Assert.Contains("X-WR-CALNAME:Bookings\r\n", output, StringComparison.Ordinal);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Events_WHEN_Serialize_THEN_OneVeventPerEventAndEveryLineCrlf(DateTime t0)
    {
        CalendarFeedEvent[] events = [Event(t0, 1), Event(t0, 2, service: "Massage")];

        string output = await Serialize("Bookings", Host, t0, events);

        Assert.Equal(2, CountOf(output, "BEGIN:VEVENT"));
        Assert.Equal(2, CountOf(output, "END:VEVENT"));
        string withoutCrlf = output.Replace("\r\n", "", StringComparison.Ordinal);
        Assert.DoesNotContain('\n', withoutCrlf);
        Assert.DoesNotContain('\r', withoutCrlf);
    }

    [Fact]
    public async Task GIVEN_UtcTimes_WHEN_Serialize_THEN_TimestampsFormattedAsUtc()
    {
        var start = new DateTime(2026, 11, 3, 14, 30, 5, DateTimeKind.Utc);
        var e = new CalendarFeedEvent(7, start, new DateTime(2026, 11, 3, 15, 45, 0, DateTimeKind.Utc),
            "Klipning", "Studio", null, null, null, null, new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc));

        string output = await Serialize("Bookings", Host, Stamp, [e]);

        Assert.Contains("DTSTAMP:20261101T080000Z\r\n", output, StringComparison.Ordinal);
        Assert.Contains("DTSTART:20261103T143005Z\r\n", output, StringComparison.Ordinal);
        Assert.Contains("DTEND:20261103T154500Z\r\n", output, StringComparison.Ordinal);
        Assert.Contains("LAST-MODIFIED:20261101T090000Z\r\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GIVEN_UnspecifiedKind_WHEN_Serialize_THEN_TreatedAsUtc()
    {
        var start = new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Unspecified);

        string output = await Serialize("Bookings", Host, Stamp, [Event(start)]);

        Assert.Contains("DTSTART:20261103T143000Z\r\n", output, StringComparison.Ordinal);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Event_WHEN_Serialize_THEN_UidIsStableBookingIdAtHost(DateTime t0)
    {
        CalendarFeedEvent e = Event(t0, 42);

        string first = await Serialize("Bookings", Host, t0, [e]);
        string second = await Serialize("Bookings", Host, t0.AddDays(1), [e]);

        Assert.Contains("UID:booking-42@booqr.example\r\n", first, StringComparison.Ordinal);
        Assert.Contains("UID:booking-42@booqr.example\r\n", second, StringComparison.Ordinal);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Event_WHEN_Serialize_THEN_SummaryIsServiceName(DateTime t0)
    {
        CalendarFeedEvent e = Event(t0, service: "Klipning");

        string output = await Serialize("Bookings", Host, t0, [e]);

        Assert.Contains("SUMMARY:Klipning\r\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GIVEN_EventWithAddress_WHEN_Serialize_THEN_LocationJoinsNonBlankParts()
    {
        CalendarFeedEvent e = Event(Stamp, location: "Studio", address1: "Nørregade 1", address2: "  ",
            zip: "1165", city: "København K");

        string output = await Serialize("Bookings", Host, Stamp, [e]);

        Assert.Contains("LOCATION:Studio\\, Nørregade 1\\, 1165 København K\r\n", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a,b", @"a\,b")]
    [InlineData("a;b", @"a\;b")]
    [InlineData(@"a\b", @"a\\b")]
    [InlineData("a\nb", @"a\nb")]
    [InlineData("a\r\nb", @"a\nb")]
    [InlineData("a\rb", @"a\nb")]
    public async Task GIVEN_SpecialCharactersInService_WHEN_Serialize_THEN_TextEscaped(string service, string escaped)
    {
        CalendarFeedEvent e = Event(Stamp, service: service);

        string output = await Serialize("Bookings", Host, Stamp, [e]);

        Assert.Contains($"SUMMARY:{escaped}\r\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GIVEN_LongAsciiSummary_WHEN_Serialize_THEN_LinesFoldedAt75Octets()
    {
        string service = new string('x', 200);
        CalendarFeedEvent e = Event(Stamp, service: service);

        string output = await Serialize("Bookings", Host, Stamp, [e]);

        string[] lines = PhysicalLines(output);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= 75));
        string summaryLine = lines.First(l => l.StartsWith("SUMMARY:", StringComparison.Ordinal));
        Assert.Equal(75, Encoding.UTF8.GetByteCount(summaryLine));
        Assert.Contains("\r\n ", output, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:" + service + "\r\n", output.Replace("\r\n ", "", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GIVEN_LongNonAsciiSummary_WHEN_Serialize_THEN_FoldsWithoutSplittingCharacters()
    {
        string service = string.Concat(Enumerable.Repeat("æøå", 20)) + "🎉🎉🎉";
        CalendarFeedEvent e = Event(Stamp, service: service);

        string output = await Serialize("Bookings", Host, Stamp, [e]);

        string[] lines = PhysicalLines(output);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= 75));
        Assert.All(lines, l => Assert.False(char.IsHighSurrogate(l[^1])));
        Assert.Contains("SUMMARY:" + service + "\r\n", output.Replace("\r\n ", "", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.DoesNotContain('�', output);
        Assert.Equal(output, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(output)));
    }

    private static CalendarFeedEvent Event(DateTime start, int id = 42, string service = "Klipning",
        string location = "Studio", string? address1 = null, string? address2 = null, string? zip = null,
        string? city = null) =>
        new(id, start, start.AddHours(1), service, location, address1, address2, zip, city, start);

    private static string[] PhysicalLines(string output) =>
        output.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    private static int CountOf(string text, string token) => text.Split(token).Length - 1;
}
