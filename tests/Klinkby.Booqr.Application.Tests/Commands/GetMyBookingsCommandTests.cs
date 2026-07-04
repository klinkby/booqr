namespace Klinkby.Booqr.Application.Tests.Commands;

public class GetMyBookingsCommandTests
{
    private readonly Mock<IMyBookingRepository> _repo = new();


    private GetMyBookingsCommand CreateSut()
    {
        return new GetMyBookingsCommand(
            _repo.Object,
            TestHelpers.TimeProvider,
            NullLogger<GetMyBookingsCommand>.Instance);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CustomerOwnsProfile_WHEN_Execute_THEN_RepositoryCalled(DateTime t0)
    {
        // Arrange
        var userId = 42;
        ClaimsPrincipal user = CreateUser(userId);
        var request = new GetMyBookingsRequest(userId, t0.AddDays(-2), t0.AddDays(2)) { User = user };

        _repo.Setup(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request,
                It.IsAny<CancellationToken>()))
            .Returns(Yield());

        GetMyBookingsCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<List<MyBooking>>.Success>(result);
        _repo.Verify(
            x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request,
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CustomerNotOwner_WHEN_Execute_THEN_ReturnsForbidden_And_DoesNotQueryRepo()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser();
        var request = new GetMyBookingsRequest(99, null, null) { User = user };
        GetMyBookingsCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        var error = Assert.IsType<Result<List<MyBooking>>.Fault>(result);
        Assert.Equal(Problem.Forbidden.Type, error.Problem.Type);
        _repo.Verify(
            x => x.GetRangeByUserId(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IPageQuery>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_Employee_WHEN_Execute_THEN_CanViewAnyUsersBookings()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(7, UserRole.Employee);
        var request = new GetMyBookingsRequest(123, null, null) { User = user };

        _repo.Setup(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request,
                It.IsAny<CancellationToken>()))
            .Returns(Yield());

        GetMyBookingsCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<List<MyBooking>>.Success>(result);
        _repo.Verify(
            x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request,
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_Admin_WHEN_Execute_THEN_CanViewAnyUsersBookings()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(8, UserRole.Admin);
        var request = new GetMyBookingsRequest(321, null, null) { User = user };

        _repo.Setup(x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request,
                It.IsAny<CancellationToken>()))
            .Returns(Yield());

        GetMyBookingsCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<List<MyBooking>>.Success>(result);
        _repo.Verify(
            x => x.GetRangeByUserId(request.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), request,
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NullFromAndTo_WHEN_Execute_THEN_DefaultsApplied(DateTime t0)
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(77, UserRole.Employee);
        var request = new GetMyBookingsRequest(999, null, null) { User = user };

        _repo.Setup(x => x.GetRangeByUserId(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), request,
                It.IsAny<CancellationToken>()))
            .Returns(Yield());

        GetMyBookingsCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<List<MyBooking>>.Success>(result);
        _repo.Verify(x => x.GetRangeByUserId(
            request.Id,
            t0.AddDays(-1),
            DateTime.MaxValue,
            request,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
