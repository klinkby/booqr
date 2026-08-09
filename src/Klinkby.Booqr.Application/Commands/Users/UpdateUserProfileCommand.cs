using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Klinkby.Booqr.Application.Commands.Users;

public record UpdateUserProfileRequest(
    [property: IgnoreDataMember]
    int Id,
    [property: Required]
    [property: StringLength(0xff)]
    string Name,
    [property: Range(45_10_00_00_00, 49_99_99_99_99)]
    long Phone
) : AuthenticatedRequest, IId;

public sealed partial class UpdateUserProfileCommand(
    IUserRepository repository,
    IActivityRecorder activityRecorder,
    IRequestMetadata etagProvider,
    ILogger<UpdateUserProfileCommand> logger
) : ICommand<UpdateUserProfileRequest, Task<Result<bool>>>
{
    private readonly LoggerMessages _log = new(logger);

    /// <summary>
    /// Executes the patch command, modifying an existing user profile in the repository.
    /// </summary>
    /// <param name="query">The authenticated request containing the patch data.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the asynchronous operation.</returns>
    public async Task<Result<bool>> Execute(UpdateUserProfileRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!query.IsOwnerOrEmployee(query.Id))
        {
            _log.CannotChangeProfile(query.AuthenticatedUserId, query.Id);
            return Problem.Forbidden with { Detail = $"You do not have access to update user {query.Id} profile." };
        }

        _log.PatchUser(query.AuthenticatedUserId, query.Id);
        PartialUser partialItem = Map(query);
        var updated = await repository.Patch(partialItem, cancellation);
        if (!updated)
        {
            return Problem.MidAirCollision with { Detail = $"User {query.Id} was already updated." };
        }

        activityRecorder.Update<User>(new(query.AuthenticatedUserId, query.Id));
        return updated;
    }

    private PartialUser Map(UpdateUserProfileRequest request) => new(request.Id)
    {
        Name = request.Name.Trim(),
        Phone = request.Phone,
        Version = etagProvider.Version
    };

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(230, LogLevel.Information, "User {UserId} patch User {Id} profile")]
        public partial void PatchUser(int userId, int id);

        [LoggerMessage(231, LogLevel.Warning, "User {UserId} is not permitted to change profile for {Id}")]
        public partial void CannotChangeProfile(int userId, int id);

    }
}
