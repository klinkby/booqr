using Klinkby.Booqr.Application.Commands.EmployeeServices;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class EmployeeServicesCommandTests
{
    private const int EmployeeUserId = 42;
    private const int OtherUserId = 99;
    private const int ServiceId = 7;

    private readonly Mock<IEmployeeServiceRepository> _repo = new();
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    // GetEmployeeServicesCommand

    [Fact]
    public void GIVEN_OwnerEmployee_WHEN_GetServices_THEN_RepositoryCalled()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(EmployeeUserId);
        var request = new GetEmployeeServicesRequest(EmployeeUserId) { User = user };
        _repo.Setup(x => x.GetByEmployeeId(EmployeeUserId, request, It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<Service>());
        var sut = new GetEmployeeServicesCommand(_repo.Object, NullLogger<GetEmployeeServicesCommand>.Instance);

        // Act
        IAsyncEnumerable<Service> _ = sut.Execute(request);

        // Assert
        _repo.Verify(x => x.GetByEmployeeId(EmployeeUserId, request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GIVEN_Employee_WHEN_GetServicesForOtherUser_THEN_RepositoryCalled()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(OtherUserId, UserRole.Employee);
        var request = new GetEmployeeServicesRequest(EmployeeUserId) { User = user };
        _repo.Setup(x => x.GetByEmployeeId(EmployeeUserId, request, It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<Service>());
        var sut = new GetEmployeeServicesCommand(_repo.Object, NullLogger<GetEmployeeServicesCommand>.Instance);

        // Act
        IAsyncEnumerable<Service> _ = sut.Execute(request);

        // Assert
        _repo.Verify(x => x.GetByEmployeeId(EmployeeUserId, request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GIVEN_Admin_WHEN_GetServicesForOtherUser_THEN_RepositoryCalled()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(OtherUserId, UserRole.Admin);
        var request = new GetEmployeeServicesRequest(EmployeeUserId) { User = user };
        _repo.Setup(x => x.GetByEmployeeId(EmployeeUserId, request, It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<Service>());
        var sut = new GetEmployeeServicesCommand(_repo.Object, NullLogger<GetEmployeeServicesCommand>.Instance);

        // Act
        IAsyncEnumerable<Service> _ = sut.Execute(request);

        // Assert
        _repo.Verify(x => x.GetByEmployeeId(EmployeeUserId, request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GIVEN_CustomerNotOwner_WHEN_GetServices_THEN_ThrowsUnauthorized_And_DoesNotQueryRepo()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(OtherUserId);
        var request = new GetEmployeeServicesRequest(EmployeeUserId) { User = user };
        var sut = new GetEmployeeServicesCommand(_repo.Object, NullLogger<GetEmployeeServicesCommand>.Instance);

        // Act + Assert
        Assert.Throws<UnauthorizedAccessException>(() => sut.Execute(request));
        _repo.Verify(x => x.GetByEmployeeId(It.IsAny<int>(), It.IsAny<IPageQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // AddEmployeeServiceCommand

    [Fact]
    public async Task GIVEN_AddRequest_WHEN_Execute_THEN_RepositoryAddCalledAndActivityRecorded()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(EmployeeUserId, UserRole.Admin);
        var request = new AddEmployeeServiceRequest(EmployeeUserId, ServiceId) { User = user };
        _repo.Setup(x => x.Add(EmployeeUserId, ServiceId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new AddEmployeeServiceCommand(_repo.Object, _activityRecorder.Object,
            NullLogger<AddEmployeeServiceCommand>.Instance);

        // Act
        await sut.Execute(request);

        // Assert
        _repo.Verify(x => x.Add(EmployeeUserId, ServiceId, CancellationToken.None), Times.Once);
        _activityRecorder.Verify(x => x.Add<EmployeeService>(
            It.Is<ActivityQuery<EmployeeService>>(q => q.UserId == EmployeeUserId && q.EntityId == EmployeeUserId)),
            Times.Once);
    }

    // DeleteEmployeeServiceCommand

    [Fact]
    public async Task GIVEN_DeleteRequest_AssignmentExists_WHEN_Execute_THEN_RepositoryDeleteCalledAndActivityRecorded()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(EmployeeUserId, UserRole.Admin);
        var request = new DeleteEmployeeServiceRequest(EmployeeUserId, ServiceId) { User = user };
        _repo.Setup(x => x.Delete(EmployeeUserId, ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new DeleteEmployeeServiceCommand(_repo.Object, _activityRecorder.Object,
            NullLogger<DeleteEmployeeServiceCommand>.Instance);

        // Act
        await sut.Execute(request);

        // Assert
        _repo.Verify(x => x.Delete(EmployeeUserId, ServiceId, CancellationToken.None), Times.Once);
        _activityRecorder.Verify(x => x.Delete<EmployeeService>(
            It.Is<ActivityQuery<EmployeeService>>(q => q.UserId == EmployeeUserId && q.EntityId == EmployeeUserId)),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_DeleteRequest_AssignmentMissing_WHEN_Execute_THEN_RepositoryDeleteCalled_NoActivity()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(EmployeeUserId, UserRole.Admin);
        var request = new DeleteEmployeeServiceRequest(EmployeeUserId, ServiceId) { User = user };
        _repo.Setup(x => x.Delete(EmployeeUserId, ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = new DeleteEmployeeServiceCommand(_repo.Object, _activityRecorder.Object,
            NullLogger<DeleteEmployeeServiceCommand>.Instance);

        // Act
        await sut.Execute(request);

        // Assert
        _repo.Verify(x => x.Delete(EmployeeUserId, ServiceId, CancellationToken.None), Times.Once);
        _activityRecorder.Verify(x => x.Delete<EmployeeService>(It.IsAny<ActivityQuery<EmployeeService>>()), Times.Never);
    }
}
