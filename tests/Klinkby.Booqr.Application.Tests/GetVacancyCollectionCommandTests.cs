using Klinkby.Booqr.Application.Calendar;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static Klinkby.Booqr.Application.Tests.TestHelpers;

namespace Klinkby.Booqr.Application.Tests;

public class GetVacancyCollectionCommandTests
{
    private readonly Mock<ICalendarRepository> _calendar = new();

    private GetVacancyCollectionCommand CreateSut(TimeProvider? timeProvider = null) =>
        new(_calendar.Object, timeProvider ?? new FakeTimeProvider());

    [Fact]
    public async Task GIVEN_PageQueryAndRange_WHEN_Execute_THEN_CallsRepositoryWithFlags_And_ReturnsItems()
    {
        // Arrange
        var page = new GetVacanciesRequest(DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(2), Start: 5, Num: 10);
        var expected = new[]
        {
            new CalendarEvent(1, 10, null, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)) { Id = 11 },
            new CalendarEvent(2, 20, null, DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3)) { Id = 22 },
        };

        _calendar.Setup(x => x.GetRange(page.FromTime!.Value, page.ToTime!.Value, page, true, false, It.IsAny<CancellationToken>()))
            .Returns(Yield(expected));

        var sut = CreateSut();

        // Act
        var result = sut.Execute(page);
        var list = await result.ToListAsync();

        // Assert
        Assert.Equal(expected.Length, list.Count);
        Assert.Equal(expected, list);
        _calendar.Verify(x => x.GetRange(page.FromTime!.Value, page.ToTime!.Value, page, true, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GIVEN_NullFromAndTo_WHEN_Execute_THEN_DefaultsApplied()
    {
        // Arrange
        var t0 = new DateTimeOffset(new DateTime(2025, 03, 01, 12, 00, 00, DateTimeKind.Utc));
        var fakeTime = new FakeTimeProvider(t0);
        var page = new GetVacanciesRequest(null, null, Start: 0, Num: 100);

        _calendar.Setup(x => x.GetRange(It.IsAny<DateTime>(), It.IsAny<DateTime>(), page, true, false, It.IsAny<CancellationToken>()))
            .Returns(Yield<CalendarEvent>());

        var sut = CreateSut(fakeTime);

        // Act
        var _ = sut.Execute(page);

        // Assert
        _calendar.Verify(x => x.GetRange(
            t0.UtcDateTime.AddDays(-1),
            DateTime.MaxValue,
            page,
            true,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => sut.Execute(null!));
    }
}
