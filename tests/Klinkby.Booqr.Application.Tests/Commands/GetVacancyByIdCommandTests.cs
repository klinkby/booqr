namespace Klinkby.Booqr.Application.Tests.Commands;

public class GetVacancyByIdCommandTests
{
    private readonly Mock<ICalendarRepository> _calendar = new();

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_VacancyExists_WHEN_Execute_THEN_ReturnsVacancy_And_CallsRepository(CalendarEvent vacancy)
    {
        // Arrange
        _calendar.Setup(x => x.GetById(vacancy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vacancy);

        var sut = new GetVacancyByIdCommand(_calendar.Object);

        // Act
        CalendarEvent? result = await sut.Execute(new ByIdRequest(vacancy.Id));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(vacancy, result);
        _calendar.Verify(x => x.GetById(vacancy.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_VacancyNotFound_WHEN_Execute_THEN_ReturnsNull()
    {
        // Arrange
        var id = 99999;
        _calendar.Setup(x => x.GetById(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEvent?)null);

        var sut = new GetVacancyByIdCommand(_calendar.Object);

        // Act
        CalendarEvent? result = await sut.Execute(new ByIdRequest(id));

        // Assert
        Assert.Null(result);
        _calendar.Verify(x => x.GetById(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
