using System.Security.Claims;
using Klinkby.Booqr.Application.Vacancies;
using Microsoft.Extensions.Logging.Abstractions;

namespace Klinkby.Booqr.Application.Tests;

public class DeleteVacancyCommandTests
{
    private readonly Mock<ICalendarRepository> _calendar = new();

    private static ClaimsPrincipal CreateUser(int id = 42)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
        return new ClaimsPrincipal(identity);
    }

    private DeleteVacancyCommand CreateSut() => new(
        _calendar.Object,
        NullLogger<DeleteVacancyCommand>.Instance);

    [Fact]
    public async Task GIVEN_VacancyHasBooking_WHEN_Execute_THEN_Throws_And_DoesNotDelete()
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(123) { User = CreateUser() };
        var vacancyWithBooking = new CalendarEvent(7, 3, 999, DateTime.UtcNow, DateTime.UtcNow.AddHours(1))
        {
            Id = request.Id
        };
        _calendar.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vacancyWithBooking);

        var sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Execute(request));
        _calendar.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_VacancyNotFound_WHEN_Execute_THEN_DeletesViaRepository()
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(456) { User = CreateUser() };
        _calendar.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEvent?)null);

        var sut = CreateSut();

        // Act
        await sut.Execute(request);

        // Assert
        _calendar.Verify(x => x.Delete(request.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_VacancyWithoutBooking_WHEN_Execute_THEN_DeletesViaRepository()
    {
        // Arrange
        var request = new AuthenticatedByIdRequest(789) { User = CreateUser() };
        var vacancy = new CalendarEvent(7, 3, null, DateTime.UtcNow, DateTime.UtcNow.AddHours(1))
        {
            Id = request.Id
        };
        _calendar.Setup(x => x.GetById(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vacancy);

        var sut = CreateSut();

        // Act
        await sut.Execute(request);

        // Assert
        _calendar.Verify(x => x.Delete(request.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
