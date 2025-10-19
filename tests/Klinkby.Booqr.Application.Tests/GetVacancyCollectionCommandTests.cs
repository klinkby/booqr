using Klinkby.Booqr.Application.Vacancies;
using static Klinkby.Booqr.Application.Tests.TestHelpers;

namespace Klinkby.Booqr.Application.Tests;

public class GetVacancyCollectionCommandTests
{
    private readonly Mock<ICalendarRepository> _calendar = new();

    private GetVacancyCollectionCommand CreateSut()
    {
        return new GetVacancyCollectionCommand(_calendar.Object, TestHelpers.TimeProvider);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_PageQueryAndRange_WHEN_Execute_THEN_CallsRepositoryWithFlags_And_ReturnsItems(DateTime t0, CalendarEvent e1, CalendarEvent e2)
    {
        // Arrange
        var page = new GetVacanciesRequest(t0.AddDays(-2), t0.AddDays(2), 5, 10);
        CalendarEvent[] expected = new[]
        {
            e1 with { EmployeeId = 1, LocationId = 10, BookingId = null, StartTime = t0, EndTime = t0.AddHours(1), Id = 11 },
            e2 with { EmployeeId = 2, LocationId = 20, BookingId = null, StartTime = t0.AddHours(2), EndTime = t0.AddHours(3), Id = 22 }
        };

        _calendar.Setup(x =>
                x.GetRange(page.FromTime!.Value, page.ToTime!.Value, page, true, false, It.IsAny<CancellationToken>()))
            .Returns(Yield(expected));

        GetVacancyCollectionCommand sut = CreateSut();

        // Act
        IAsyncEnumerable<CalendarEvent> result = sut.Execute(page);
        List<CalendarEvent> list = await result.ToListAsync();

        // Assert
        Assert.Equal(expected.Length, list.Count);
        Assert.Equal(expected, list);
        _calendar.Verify(
            x => x.GetRange(page.FromTime!.Value, page.ToTime!.Value, page, true, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_NullFromAndTo_WHEN_Execute_THEN_DefaultsApplied(DateTime t0)
    {
        // Arrange
        var page = new GetVacanciesRequest(null, null);

        _calendar.Setup(x => x.GetRange(It.IsAny<DateTime>(), It.IsAny<DateTime>(), page, true, false,
                It.IsAny<CancellationToken>()))
            .Returns(Yield<CalendarEvent>());

        GetVacancyCollectionCommand sut = CreateSut();

        // Act
        IAsyncEnumerable<CalendarEvent> _ = sut.Execute(page);

        // Assert
        _calendar.Verify(x => x.GetRange(
            t0.AddDays(-1),
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
        GetVacancyCollectionCommand sut = CreateSut();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => sut.Execute(null!));
    }
}
