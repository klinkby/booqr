namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record GetUserCollectionRequest : IPageQuery
{
    [MaxLength(0xff)]
    public string? K { get; init; }

    [RegularExpression($"{UserRole.Admin}|{UserRole.Employee}|{UserRole.Customer}")]
    public string? Role { get; init; }

    [Range(1, 1000)]
    public int? Num { get; init; } = 100;

    [Range(0, int.MaxValue)]
    public int? Start { get; init; } = 0;
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
