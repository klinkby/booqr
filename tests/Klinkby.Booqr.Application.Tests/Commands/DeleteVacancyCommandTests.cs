namespace Klinkby.Booqr.Application.Tests.Commands;

public class DeleteVacancyCommandTests
{
    private readonly Mock<ICalendarRepository> _calendar = new();
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    private DeleteVacancyCommand CreateSut()
    {
        return new DeleteVacancyCommand(
            _calendar.Object,
            _activityRecorder.Object,
            NullLogger<DeleteVacancyCommand>.Instance);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_VacancyHasBooking_WHEN_Execute_THEN_ReturnsConflictFault_And_DoesNotDelete(DateTime t0, CalendarEvent autoVacancy)
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(123) { User = CreateUser() };
        var vacancyWithBooking = autoVacancy with { BookingId = 999, StartTime = t0, EndTime = t0.AddHours(1), Id = request.Id };
        _calendar.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vacancyWithBooking);

        DeleteVacancyCommand sut = CreateSut();

        // Act
        Result<bool> result = await sut.Execute(request);

        // Assert
        var fault = Assert.IsType<Result<bool>.Fault>(result);
        Assert.Equal(Problem.Conflict.Type, fault.Problem.Type);
        _calendar.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _activityRecorder.Verify(x => x.Delete(It.IsAny<ActivityQuery<CalendarEvent>>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_VacancyNotFound_WHEN_Execute_THEN_DeletesViaRepository()
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(456) { User = CreateUser() };
        _calendar.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEvent?)null);

        DeleteVacancyCommand sut = CreateSut();

        // Act
        await sut.Execute(request);

        // Assert
        _calendar.Verify(x => x.Delete(request.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_VacancyWithoutBooking_WHEN_Execute_THEN_DeletesViaRepository(DateTime t0, CalendarEvent autoVacancy)
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(789) { User = CreateUser() };
        var vacancy = autoVacancy with { BookingId = null, StartTime = t0, EndTime = t0.AddHours(1), Id = request.Id };
        _calendar.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vacancy);

        DeleteVacancyCommand sut = CreateSut();

        // Act
        await sut.Execute(request);

        // Assert
        _calendar.Verify(x => x.Delete(request.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
