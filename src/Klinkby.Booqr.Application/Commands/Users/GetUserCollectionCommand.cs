namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record GetUserCollectionRequest(int? Start, int? Num) : PageQuery(Start, Num)
{
    [StringLength(0xff)]
    public string? K { get; init; }

    [RegularExpression($"{UserRole.Admin}|{UserRole.Employee}|{UserRole.Customer}")]
    public string? Role { get; init; }
}

public sealed class GetUserCollectionCommand(
    IUserRepository users)
    : ICommand<GetUserCollectionRequest, IAsyncEnumerable<User>>
{
    public IAsyncEnumerable<User> Execute(
        GetUserCollectionRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return users.Find(
            query.K is {Length: 0} ? null : query.K ,
            query.Role is {Length: 0} ? null : query.Role,
            query,
            cancellation);
    }
};
