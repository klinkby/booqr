namespace Klinkby.Booqr.Application.Commands.Users;

/// <inheritdoc />
public sealed partial class DeleteUserCommand(
    IUserRepository users,
    IActivityRecorder activityRecorder,
    ILogger<DeleteUserCommand> logger)
    : DeleteCommand<User>(users, activityRecorder, logger)
{
    private readonly LoggerMessages _log = new(logger);

    internal override Task<bool> Delete(AuthenticatedByIdRequest query, CancellationToken cancellation)
    {
        if (query.AuthenticatedUserId == query.Id)
        {
            _log.Harakiri(query.AuthenticatedUserId);
            return Task.FromResult(false);
        }

        return base.Delete(query, cancellation);
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(240, LogLevel.Warning, "User {UserId} attempted self-terminating")]
        internal partial void Harakiri(int userId);
    }
}
