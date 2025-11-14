namespace Klinkby.Booqr.Application.Commands.Users;

public sealed class GetUserByIdCommand(
    IUserRepository users)
    : GetByIdCommand<User>(users);
