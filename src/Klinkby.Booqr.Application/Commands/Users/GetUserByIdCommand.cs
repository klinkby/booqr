using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record GetUserByIdRequest([property: Range(1, int.MaxValue)] int Id)
    : AuthenticatedRequest, IId;

// ASVS 8.2.2: data-level access control. A plain Customer may read only their own
// user record or the record of a staff member (Employee/Admin) profile; reading
// another customer's record is forbidden to mitigate IDOR/BOLA. Employees/Admins are
// unrestricted. The route policy (Customer) only gates function-level access; the
// object-level rule is enforced here after loading the target so its role is known.
public sealed partial class GetUserByIdCommand(
    IUserRepository users,
    ILogger<GetUserByIdCommand> logger)
    : ICommand<GetUserByIdRequest, Task<Result<User>>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<User>> Execute(GetUserByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        User? value = await users.GetById(query.Id, cancellation);
        if (value is null)
        {
            return Problem.NotFound with { Detail = $"{nameof(User)} {query.Id} was not found" };
        }

        // Staff see anyone; a customer may see themselves or any staff profile, but not
        // another customer.
        bool authorized = query.IsStaffOrOwner(query.Id) || value.IsStaff;
        if (!authorized)
        {
            _log.CannotInspectUser(query.AuthenticatedUserId, query.Id);
            return Problem.Forbidden;
        }

        return value;
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(121, LogLevel.Warning,
            "User {UserId} is not permitted to inspect user {Id}")]
        public partial void CannotInspectUser(int userId, int id);
    }
}
