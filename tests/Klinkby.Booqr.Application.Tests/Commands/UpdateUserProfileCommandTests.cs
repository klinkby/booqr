using Klinkby.Booqr.Application.Abstractions;
using Klinkby.Booqr.Core.Exceptions;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class UpdateUserProfileCommandTests
{
    private readonly Mock<IRequestMetadata> _etagProvider = new();
    private readonly Mock<IUserRepository> _repo = new();
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    private UpdateUserProfileCommand CreateSut() =>
        new(_repo.Object, _activityRecorder.Object, _etagProvider.Object, NullLogger<UpdateUserProfileCommand>.Instance);

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        UpdateUserProfileCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Execute(null!));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidRequest_WHEN_Execute_THEN_PatchesUserInRepository(
        ClaimsPrincipal user,
        UpdateUserProfileRequest request)
    {
        // Arrange
        _repo.Setup(x => x.Patch(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(true);

        UpdateUserProfileCommand sut = CreateSut();
        request = request with { User = user, Name = $"  {request.Name}  " };

        // Act
        await sut.Execute(request);

        // Assert
        _repo.Verify(
            x => x.Patch(
                It.Is<PartialUser>(p =>
                    p.Id == request.Id &&
                    p.Name == request.Name.Trim() &&
                    p.Phone == request.Phone &&
                    p.Version == _etagProvider.Object.Version),
                CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_RepositoryUpdateFails_WHEN_Execute_THEN_ThrowsMidAirCollisionException(
        ClaimsPrincipal user,
        UpdateUserProfileRequest request)
    {
        // Arrange
        _repo.Setup(x => x.Patch(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(false);

        UpdateUserProfileCommand sut = CreateSut();
        request = request with { User = user };

        // Act + Assert
        MidAirCollisionException ex = await Assert.ThrowsAsync<MidAirCollisionException>(() => sut.Execute(request));
        Assert.Contains($"User {request.Id}", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidRequest_WHEN_Execute_THEN_RecordsActivity(
        ClaimsPrincipal user,
        UpdateUserProfileRequest request)
    {
        // Arrange
        _repo.Setup(x => x.Patch(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(true);

        UpdateUserProfileCommand sut = CreateSut();
        request = request with { User = user };

        // Act
        await sut.Execute(request);

        // Assert
        _activityRecorder.Verify(
            x => x.Update(It.IsAny<ActivityQuery<User>>()),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UpdateFails_WHEN_Execute_THEN_DoesNotRecordActivity(
        ClaimsPrincipal user,
        UpdateUserProfileRequest request)
    {
        // Arrange
        _repo.Setup(x => x.Patch(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(false);

        UpdateUserProfileCommand sut = CreateSut();
        request = request with { User = user };

        // Act + Assert
        await Assert.ThrowsAsync<MidAirCollisionException>(() => sut.Execute(request));
        _activityRecorder.Verify(
            x => x.Update(It.IsAny<ActivityQuery<User>>()),
            Times.Never);
    }
}
