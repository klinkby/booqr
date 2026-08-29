using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record GetUserCollectionRequest : AuthenticatedRequest, IPageQuery
{
    [StringLength(0xff)]
    public string? K { get; init; }

    [RegularExpression($"{UserRole.Admin}|{UserRole.Employee}|{UserRole.Customer}")]
    public string? Role { get; init; }

    [Range(0, int.MaxValue)]
    public int? Start { get; init; } = 0;

    [Range(1, 1000)]
    public int? Num { get; init; } = 100;
}

// ASVS 8.2.2: data-level access control. Staff (Employee/Admin) may list any users. A
// plain Customer may only enumerate employees (Role=Employee) so they can browse bookable
// staff; listing customers or all users is forbidden to prevent enumeration of other
// customers. The route policy (Customer) only gates function-level access.
public sealed partial class GetUserCollectionCommand(
    IUserRepository users,
    ILogger<GetUserCollectionCommand> logger)
    : ICommand<GetUserCollectionRequest, Task<Result<List<User>>>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<List<User>>> Execute(
        GetUserCollectionRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        string? role = query.Role is { Length: 0 } ? null : query.Role;

        if (!query.IsStaff&& role != UserRole.Employee)
        {
            _log.CannotListUsers(query.AuthenticatedUserId, role ?? "(any)");
            return Problem.Forbidden;
        }

        List<User> list = await users.Find(
                query.K is { Length: 0 } ? null : query.K,
                role,
                query,
                cancellation)
            .ToListAsync(cancellation);

        return list;
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(122, LogLevel.Warning,
            "User {UserId} is not permitted to list users with role {Role}")]
        public partial void CannotListUsers(int userId, string role);
    }
}
