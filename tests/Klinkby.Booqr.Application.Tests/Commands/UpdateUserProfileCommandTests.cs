using System.ComponentModel.DataAnnotations;
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
        UpdateUserProfileRequest request)
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(request.Id, UserRole.Customer);
        _repo.Setup(x => x.UpdateProfile(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(true);

        UpdateUserProfileCommand sut = CreateSut();
        request = request with { User = user, Name = $"  {request.Name}  " };

        // Act
        Result<bool> result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<bool>.Success>(result);
        _repo.Verify(
            x => x.UpdateProfile(
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
    public async Task GIVEN_RepositoryUpdateFails_WHEN_Execute_THEN_ReturnsMidAirCollisionFault(
        UpdateUserProfileRequest request)
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(request.Id, UserRole.Customer);
        _repo.Setup(x => x.UpdateProfile(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(false);

        UpdateUserProfileCommand sut = CreateSut();
        request = request with { User = user };

        // Act
        Result<bool> result = await sut.Execute(request);

        // Assert
        Result<bool>.Fault err =  Assert.IsType<Result<bool>.Fault>(result);
        Assert.Contains($"User {request.Id}", err.Problem.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidRequest_WHEN_Execute_THEN_RecordsActivity(
        UpdateUserProfileRequest request)
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(request.Id, UserRole.Customer);
        _repo.Setup(x => x.UpdateProfile(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(true);

        UpdateUserProfileCommand sut = CreateSut();
        request = request with { User = user };

        // Act
        Result<bool> result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<bool>.Success>(result);
        _activityRecorder.Verify(
            x => x.Update(It.IsAny<ActivityQuery<User>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UpdateFails_WHEN_Execute_THEN_DoesNotRecordActivity(
        UpdateUserProfileRequest request)
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(request.Id, UserRole.Customer);
        _repo.Setup(x => x.UpdateProfile(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(false);

        UpdateUserProfileCommand sut = CreateSut();
        request = request with { User = user };

        // Act
        Result<bool> result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<bool>.Fault>(result);
        _activityRecorder.Verify(
            x => x.Update(It.IsAny<ActivityQuery<User>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_JsonBodyWithRole_WHEN_Execute_THEN_PatchDoesNotChangeRole(
        UpdateUserProfileRequest request)
    {
        // Arrange: a customer smuggles "role" into the profile body (mass assignment).
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            name = request.Name,
            phone = request.Phone,
            role = UserRole.Admin
        });
        UpdateUserProfileRequest bound = System.Text.Json.JsonSerializer.Deserialize<UpdateUserProfileRequest>(
            json, System.Text.Json.JsonSerializerOptions.Web)!;
        bound = bound with { Id = request.Id, User = CreateUser(request.Id, UserRole.Customer) };
        _repo.Setup(x => x.UpdateProfile(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(true);
        UpdateUserProfileCommand sut = CreateSut();

        // Act
        Result<bool> result = await sut.Execute(bound);

        // Assert: the COALESCE patch leaves role untouched only when Role is null.
        Assert.IsType<Result<bool>.Success>(result);
        _repo.Verify(
            x => x.UpdateProfile(It.Is<PartialUser>(p => p.Id == request.Id && p.Role == null), CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NullPhone_WHEN_Execute_THEN_ClearsPhone(UpdateUserProfileRequest request)
    {
        // Arrange
        request = request with { Phone = null, User = CreateUser(request.Id, UserRole.Customer) };
        _repo.Setup(x => x.UpdateProfile(It.IsAny<PartialUser>(), CancellationToken.None))
            .ReturnsAsync(true);
        UpdateUserProfileCommand sut = CreateSut();

        // Act
        Result<bool> result = await sut.Execute(request);

        // Assert
        Assert.IsType<Result<bool>.Success>(result);
        _repo.Verify(
            x => x.UpdateProfile(It.Is<PartialUser>(p => p.Id == request.Id && p.Phone == null), CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    public void GIVEN_NullOrBlankPhoneJson_WHEN_Deserialize_THEN_PhoneIsNullAndValid(string phoneJson)
    {
        UpdateUserProfileRequest? request = System.Text.Json.JsonSerializer.Deserialize<UpdateUserProfileRequest>(
            $$"""{"name":"n","phone":{{phoneJson}}}""", System.Text.Json.JsonSerializerOptions.Web);

        Assert.NotNull(request);
        Assert.Null(request.Phone);
        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Theory]
    [InlineData(3_521_000L, true)]
    [InlineData(49_999_999_999_999_999L, true)]
    [InlineData(3_520_999L, false)]
    [InlineData(60_000_000_000_000_000L, false)]
    public void GIVEN_PhoneNumber_WHEN_Validate_THEN_RangeEnforced(long phone, bool expected)
    {
        UpdateUserProfileRequest request = new(1, "n", phone);

        Assert.Equal(expected,
            Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
