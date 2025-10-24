namespace Klinkby.Booqr.Application.Commands.Users;

public sealed class GetUserCollectionCommand(
    IUserRepository users)
    : GetCollectionCommand<PageQuery, User>(users);
