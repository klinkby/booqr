namespace Klinkby.Booqr.Application.Users;

public sealed class GetUserByIdCommand(
    IUserRepository users)
    : GetByIdCommand<User>(users);
