using System.Runtime.Serialization;
using Klinkby.Booqr.Core.Exceptions;

namespace Klinkby.Booqr.Application.Commands.Users;

public record UpdateUserProfileRequest(
    [property: Range(1, int.MaxValue)]
    [property: IgnoreDataMember]
    int Id,
    [property: Required]
    [property: StringLength(0xff)]
    string Name,
    [property: Range(10_00_00_00, 99_99_99_99)]
    long Phone
) : AuthenticatedRequest, IId;

public sealed partial class UpdateUserProfileCommand(
    IUserRepository repository,
    IActivityRecorder activityRecorder,
    IRequestMetadata etagProvider,
    ILogger<UpdateUserProfileCommand> logger
) : ICommand<UpdateUserProfileRequest>
{
    private readonly LoggerMessages _log = new(logger);

    /// <summary>
    /// Executes the patch command, modifying an existing user profile in the repository.
    /// </summary>
    /// <param name="query">The authenticated request containing the patch data.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <exception cref="MidAirCollisionException">Thrown when the entity was modified by another operation (optimistic concurrency failure).</exception>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Execute(UpdateUserProfileRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!query.IsOwnerOrEmployee(query.Id))
        {
            FailUnauthorized(query);
        }

        _log.PatchUser(query.AuthenticatedUserId, query.Id);
        PartialUser partialItem = Map(query);
        var updated = await repository.Patch(partialItem, cancellation);
        if (!updated)
        {
            throw new MidAirCollisionException($"User {query.Id} was already updated.");
        }

        activityRecorder.Update<User>(new(query.AuthenticatedUserId, query.Id));
    }

    private void FailUnauthorized(UpdateUserProfileRequest query)
    {
        _log.CannotChangeProfile(query.AuthenticatedUserId, query.Id);
        throw new UnauthorizedAccessException("You do not have access to update this user profile.");
    }

    private PartialUser Map(UpdateUserProfileRequest request) => new(request.Id)
    {
        Name = request.Name.Trim(),
        Phone = request.Phone,
        Version = etagProvider.Version
    };

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(230, LogLevel.Information, "User {UserId} patch User {Id} profile")]
        public partial void PatchUser(int userId, int id);

        [LoggerMessage(231, LogLevel.Warning, "User {UserId} is not permitted to change profile for {Id}")]
        public partial void CannotChangeProfile(int userId, int id);

    }
}
