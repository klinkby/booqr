using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Klinkby.Booqr.Application.Commands.Users;

public record ChangeUserRoleRequest(
    [property: IgnoreDataMember]
    int Id,
    [property: Required]
    [property: RegularExpression($"^({UserRole.Customer}|{UserRole.Employee}|{UserRole.Admin})$")]
    string Role
) : AuthenticatedRequest, IId;

/// <summary>
///     Admin-only change of another user's role. Re-authorizes the caller against the database
///     (the JWT may be stale) while holding a lock on all admin rows, which serializes concurrent
///     role changes so the last admin can never be removed.
/// </summary>
public sealed partial class ChangeUserRoleCommand(
    IUserRepository users,
    ICalendarRepository calendar,
    IRefreshTokenRepository refreshTokens,
    ITransaction transaction,
    IActivityRecorder activityRecorder,
    IRequestMetadata etagProvider,
    TimeProvider timeProvider,
    ILogger<ChangeUserRoleCommand> logger
) : ICommand<ChangeUserRoleRequest, Task<Result<bool>>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<bool>> Execute(ChangeUserRoleRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.AuthenticatedUserId == query.Id)
        {
            _log.CannotChangeOwnRole(query.AuthenticatedUserId);
            return Problem.Forbidden with { Detail = "You cannot change your own role." };
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        string fromRole;

        await transaction.Begin(cancellation);
        try
        {
            // Locks all admin rows: re-authorizes the caller against the DB and serializes role changes.
            IReadOnlyList<int> admins = await users.LockAdmins(cancellation);
            if (!admins.Contains(query.AuthenticatedUserId))
            {
                await transaction.Rollback(cancellation);
                _log.CallerNotAdmin(query.AuthenticatedUserId, query.Id);
                return Problem.Forbidden with { Detail = "You are not permitted to change user roles." };
            }

            User? target = await users.GetByIdForUpdate(query.Id, cancellation);
            if (target is null)
            {
                await transaction.Rollback(cancellation);
                return Problem.NotFound with { Detail = $"User {query.Id} was not found." };
            }

            fromRole = target.Role;
            if (string.Equals(fromRole, query.Role, StringComparison.Ordinal))
            {
                await transaction.Commit(cancellation);
                return true;
            }

            if (query.Role == UserRole.Customer
                && (fromRole == UserRole.Employee || fromRole == UserRole.Admin)
                && await calendar.HasFutureEvents(query.Id, now, cancellation))
            {
                await transaction.Rollback(cancellation);
                _log.HasFutureEvents(query.AuthenticatedUserId, query.Id);
                return Problem.Conflict with
                {
                    Detail = $"User {query.Id} has future calendar entries that must be removed before changing the role to {UserRole.Customer}."
                };
            }

            // Only Role and Version: all other fields stay untouched by the COALESCE patch.
            PartialUser patch = new(query.Id) { Role = query.Role, Version = etagProvider.Version };
            if (!await users.Patch(patch, cancellation))
            {
                await transaction.Rollback(cancellation);
                return Problem.MidAirCollision with { Detail = $"User {query.Id} was already updated." };
            }

            await refreshTokens.RevokeAllForUser(query.Id, now, cancellation);
            await transaction.Commit(cancellation);
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }

        // After commit: a swallowed DbException inside a PostgreSQL transaction would abort it.
        await activityRecorder.ChangeRole<User>(new(query.AuthenticatedUserId, query.Id, query.TenantId), cancellation);
        _log.RoleChanged(query.AuthenticatedUserId, query.Id, fromRole, query.Role);
        return true;
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(250, LogLevel.Warning, "User {UserId} is not permitted to change their own role")]
        public partial void CannotChangeOwnRole(int userId);

        [LoggerMessage(251, LogLevel.Warning, "User {UserId} is not an admin (stale token?) and cannot change role of user {Id}")]
        public partial void CallerNotAdmin(int userId, int id);

        [LoggerMessage(252, LogLevel.Warning, "User {UserId} cannot demote user {Id} to customer: has future calendar entries")]
        public partial void HasFutureEvents(int userId, int id);

        [LoggerMessage(253, LogLevel.Warning, "User {UserId} changed role of user {Id} from {FromRole} to {ToRole}")]
        public partial void RoleChanged(int userId, int id, string fromRole, string toRole);
    }
}
