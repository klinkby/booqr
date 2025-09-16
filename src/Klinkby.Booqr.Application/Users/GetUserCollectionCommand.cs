namespace Klinkby.Booqr.Application.Users;

public sealed class GetUserCollectionCommand(
    IUserRepository users)
    : GetCollectionCommand<PageQuery, User>(users);
