using Klinkby.Booqr.Application.Abstractions;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class ChangeUserRoleCommandTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICalendarRepository> _calendar = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ITransaction> _transaction = new();
    private readonly Mock<IActivityRecorder> _activityRecorder = new();
    private readonly Mock<IRequestMetadata> _etagProvider = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _timeProvider = TestHelpers.TimeProvider;

    private ChangeUserRoleCommand CreateSut() =>
        new(_users.Object, _calendar.Object, _refreshTokens.Object, _transaction.Object,
            _activityRecorder.Object, _etagProvider.Object, _timeProvider,
            NullLogger<ChangeUserRoleCommand>.Instance);

    /// <summary>Builds a request from an admin (different id than the target) and arranges the admin lock.</summary>
    private ChangeUserRoleRequest Arrange(ChangeUserRoleRequest request, int callerId, string toRole,
        bool callerIsAdmin = true)
    {
        _users.Setup(x => x.LockAdmins(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<int>)(callerIsAdmin ? new List<int> { callerId } : new List<int>()));
        return request with { User = CreateUser(callerId, UserRole.Admin), Role = toRole };
    }

    private void ArrangeTarget(ChangeUserRoleRequest request, User user, string fromRole) =>
        _users.Setup(x => x.GetByIdForUpdate(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user with { Id = request.Id, Role = fromRole });

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        ChangeUserRoleCommand sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Execute(null!));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_SelfChange_WHEN_Execute_THEN_ForbiddenAndNoRepositoryCalls(
        ChangeUserRoleRequest request)
    {
        request = request with { User = CreateUser(request.Id, UserRole.Admin), Role = UserRole.Employee };
        ChangeUserRoleCommand sut = CreateSut();

        Result<bool> result = await sut.Execute(request);

        Result<bool>.Fault fault = Assert.IsType<Result<bool>.Fault>(result);
        Assert.Equal(Problem.Forbidden.HttpStatusCode, fault.Problem.HttpStatusCode);
        _transaction.Verify(x => x.Begin(It.IsAny<CancellationToken>()), Times.Never);
        _users.VerifyNoOtherCalls();
        _calendar.VerifyNoOtherCalls();
        _refreshTokens.VerifyNoOtherCalls();
        _activityRecorder.VerifyNoOtherCalls();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_CallerNotInLockedAdmins_WHEN_Execute_THEN_ForbiddenAndRollback(
        ChangeUserRoleRequest request)
    {
        int callerId = request.Id + 1;
        request = Arrange(request, callerId, UserRole.Employee, callerIsAdmin: false);
        ChangeUserRoleCommand sut = CreateSut();

        Result<bool> result = await sut.Execute(request);

        Result<bool>.Fault fault = Assert.IsType<Result<bool>.Fault>(result);
        Assert.Equal(Problem.Forbidden.HttpStatusCode, fault.Problem.HttpStatusCode);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.GetByIdForUpdate(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.VerifyNoOtherCalls();
        _activityRecorder.VerifyNoOtherCalls();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_TargetNotFound_WHEN_Execute_THEN_NotFoundAndRollback(
        ChangeUserRoleRequest request)
    {
        int callerId = request.Id + 1;
        request = Arrange(request, callerId, UserRole.Employee);
        _users.Setup(x => x.GetByIdForUpdate(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        ChangeUserRoleCommand sut = CreateSut();

        Result<bool> result = await sut.Execute(request);

        Result<bool>.Fault fault = Assert.IsType<Result<bool>.Fault>(result);
        Assert.Equal(Problem.NotFound.HttpStatusCode, fault.Problem.HttpStatusCode);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_SameRole_WHEN_Execute_THEN_CommitsWithoutPatchRevokeOrActivity(
        ChangeUserRoleRequest request, User user)
    {
        int callerId = request.Id + 1;
        request = Arrange(request, callerId, UserRole.Employee);
        ArrangeTarget(request, user, UserRole.Employee);
        ChangeUserRoleCommand sut = CreateSut();

        Result<bool> result = await sut.Execute(request);

        Assert.IsType<Result<bool>.Success>(result);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.VerifyNoOtherCalls();
        _activityRecorder.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineAutoData(UserRole.Employee)]
    [InlineAutoData(UserRole.Admin)]
    public async Task GIVEN_StaffWithFutureEvents_WHEN_DemoteToCustomer_THEN_ConflictAndRollback(
        string fromRole, ChangeUserRoleRequest request, User user)
    {
        int callerId = request.Id + 1;
        request = Arrange(request, callerId, UserRole.Customer);
        ArrangeTarget(request, user, fromRole);
        _calendar.Setup(x => x.HasFutureEvents(request.Id, _timeProvider.GetUtcNow().UtcDateTime,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        ChangeUserRoleCommand sut = CreateSut();

        Result<bool> result = await sut.Execute(request);

        Result<bool>.Fault fault = Assert.IsType<Result<bool>.Fault>(result);
        Assert.Equal(Problem.Conflict.HttpStatusCode, fault.Problem.HttpStatusCode);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.VerifyNoOtherCalls();
        _activityRecorder.VerifyNoOtherCalls();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AdminDemotedToEmployee_WHEN_Execute_THEN_DoesNotCheckFutureEvents(
        ChangeUserRoleRequest request, User user)
    {
        int callerId = request.Id + 1;
        request = Arrange(request, callerId, UserRole.Employee);
        ArrangeTarget(request, user, UserRole.Admin);
        _users.Setup(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ChangeUserRoleCommand sut = CreateSut();

        Result<bool> result = await sut.Execute(request);

        Assert.IsType<Result<bool>.Success>(result);
        _calendar.Verify(x => x.HasFutureEvents(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_PatchFails_WHEN_Execute_THEN_MidAirCollisionRollbackNoRevoke(
        ChangeUserRoleRequest request, User user)
    {
        int callerId = request.Id + 1;
        request = Arrange(request, callerId, UserRole.Employee);
        ArrangeTarget(request, user, UserRole.Customer);
        _users.Setup(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        ChangeUserRoleCommand sut = CreateSut();

        Result<bool> result = await sut.Execute(request);

        Result<bool>.Fault fault = Assert.IsType<Result<bool>.Fault>(result);
        Assert.Equal(Problem.MidAirCollision.HttpStatusCode, fault.Problem.HttpStatusCode);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.VerifyNoOtherCalls();
        _activityRecorder.VerifyNoOtherCalls();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidChange_WHEN_Execute_THEN_PatchesRoleOnlyRevokesCommitsThenRecordsActivity(
        ChangeUserRoleRequest request, User user)
    {
        int callerId = request.Id + 1;
        request = Arrange(request, callerId, UserRole.Employee);
        ArrangeTarget(request, user, UserRole.Customer);
        List<string> calls = [];
        _users.Setup(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("patch")).ReturnsAsync(true);
        _refreshTokens.Setup(x => x.RevokeAllForUser(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("revoke")).ReturnsAsync(1);
        _transaction.Setup(x => x.Commit(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("commit")).Returns(ValueTask.CompletedTask);
        _activityRecorder.Setup(x => x.ChangeRole(It.IsAny<ActivityQuery<User>>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("activity")).Returns(Task.CompletedTask);
        ChangeUserRoleCommand sut = CreateSut();

        Result<bool> result = await sut.Execute(request);

        Assert.IsType<Result<bool>.Success>(result);
        Assert.Equal(["patch", "revoke", "commit", "activity"], calls);
        _users.Verify(x => x.Patch(
                It.Is<PartialUser>(p =>
                    p.Id == request.Id &&
                    p.Role == request.Role &&
                    p.Version == _etagProvider.Object.Version &&
                    p.Email == null && p.PasswordHash == null && p.Name == null && p.Phone == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _refreshTokens.Verify(x => x.RevokeAllForUser(request.Id, _timeProvider.GetUtcNow().UtcDateTime,
            It.IsAny<CancellationToken>()), Times.Once);
        _activityRecorder.Verify(x => x.ChangeRole(
                It.Is<ActivityQuery<User>>(q => q.UserId == callerId && q.EntityId == request.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_PatchThrows_WHEN_Execute_THEN_RollsBackAndRethrows(
        ChangeUserRoleRequest request, User user, InvalidOperationException error)
    {
        int callerId = request.Id + 1;
        request = Arrange(request, callerId, UserRole.Employee);
        ArrangeTarget(request, user, UserRole.Customer);
        _users.Setup(x => x.Patch(It.IsAny<PartialUser>(), It.IsAny<CancellationToken>())).ThrowsAsync(error);
        ChangeUserRoleCommand sut = CreateSut();

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Execute(request));

        Assert.Same(error, thrown);
        _transaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.VerifyNoOtherCalls();
        _activityRecorder.VerifyNoOtherCalls();
    }
}
