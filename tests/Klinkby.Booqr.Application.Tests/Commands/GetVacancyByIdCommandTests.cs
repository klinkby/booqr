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
        var result = await sut.Execute(new ByIdRequest(vacancy.Id));

        // Assert
        var success = Assert.IsType<Result<CalendarEvent>.Success>(result);
        Assert.Equal(vacancy, success.Value);
        _calendar.Verify(x => x.GetById(vacancy.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_VacancyNotFound_WHEN_Execute_THEN_ReturnsNotFound()
    {
        // Arrange
        var id = 99999;
        _calendar.Setup(x => x.GetById(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEvent?)null);

        var sut = new GetVacancyByIdCommand(_calendar.Object);

        // Act
        var result = await sut.Execute(new ByIdRequest(id));

        // Assert
        var error = Assert.IsType<Result<CalendarEvent>.Fault>(result);
        Assert.Equal(Problem.NotFound.Type, error.Problem.Type);
        _calendar.Verify(x => x.GetById(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
