using Klinkby.Booqr.Application.Util;

namespace Klinkby.Booqr.Application.Tests.Util;

public class MailTimeTests
{
    [Theory]
    [InlineData("2026-01-15T13:30:00", "torsdag 15. jan kl. 14:30")] // CET, UTC+1
    [InlineData("2026-07-15T13:30:00", "onsdag 15. jul kl. 15:30")] // CEST, UTC+2
    [InlineData("2026-07-15T22:30:00", "torsdag 16. jul kl. 00:30")] // crosses midnight, 24h clock
    public void GIVEN_UtcStart_WHEN_FormatStart_THEN_DanishLocalTime(string utc, string expected)
    {
        var start = DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, MailTime.FormatStart(start));
    }

    [Theory]
    [InlineData("2026-01-15", "2026-01-14T23:00:00", "2026-01-15T23:00:00")] // CET
    [InlineData("2026-07-15", "2026-07-14T22:00:00", "2026-07-15T22:00:00")] // CEST
    [InlineData("2026-03-29", "2026-03-28T23:00:00", "2026-03-29T22:00:00")] // spring forward, 23h
    [InlineData("2026-10-25", "2026-10-24T22:00:00", "2026-10-25T23:00:00")] // fall back, 25h
    public void GIVEN_DanishDate_WHEN_DayRangeUtc_THEN_MidnightToMidnightDanishTime(string date, string from, string to)
    {
        var (f, t) = MailTime.DayRangeUtc(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture), f);
        Assert.Equal(DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture), t);
    }

    [Fact]
    public void GIVEN_UtcAfterDanishMidnight_WHEN_LocalDate_THEN_NextDay() =>
        Assert.Equal(new DateOnly(2026, 7, 16), MailTime.LocalDate(new DateTime(2026, 7, 15, 22, 30, 0, DateTimeKind.Utc)));
}
