namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record GetUserCollectionRequest(
    [StringLength(0xff)] string? K = null,
    [RegularExpression($"{UserRole.Admin}|{UserRole.Employee}|{UserRole.Customer}")] string? Role = null,
    int? Start = 0,
    int? Num = 100) : PageQuery(Start, Num);

public sealed class GetUserCollectionCommand(
    IUserRepository users)
    : ICommand<GetUserCollectionRequest, Task<List<User>>>
{
    public Task<List<User>> Execute(
        GetUserCollectionRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return users.Find(
                query.K is { Length: 0 } ? null : query.K,
                query.Role is { Length: 0 } ? null : query.Role,
                query,
                cancellation)
            .ToListAsync(cancellation)
            .AsTask();
    }
};
