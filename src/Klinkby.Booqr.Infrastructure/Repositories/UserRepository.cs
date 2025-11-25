using Klinkby.Booqr.Infrastructure.Services;

namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("email", "passwordhash", "role", "name", "phone")]
internal sealed partial class UserRepository(IConnectionProvider connectionProvider, TimeProvider timeProvider)
    : AuditRepository<User>(timeProvider), IUserRepository
{
    private const string TableName = "users";

    public async Task<User?> GetByEmail(string email, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QueryFirstOrDefaultAsync<User>(
            $"""
             SELECT id,{CommaSeparated},created,modified
             FROM {TableName}
             WHERE deleted IS NULL AND email = @email
             """,
            new { email });
    }


    #region IRepository

    /// <inheritdoc />
    public async IAsyncEnumerable<User> GetAll(IPageQuery pageQuery,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<User> query = connection.QueryUnbufferedAsync<User>($"{GetAllQuery}", pageQuery);
        await foreach (User item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    public async Task<User?> GetById(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QuerySingleOrDefaultAsync<User>($"{GetByIdQuery}", new GetByIdParameters(id));
    }

    public async Task<int> Add(User newItem, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        var result = await connection.ExecuteScalarAsync($"{InsertQuery}", WithCreated(newItem));
        Debug.Assert(result is int);
        return (int)result;
    }

    public async Task<bool> Update(User item, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync($"{PatchQuery}", WithModified(item));
    }

    public async Task<bool> Patch(PartialUser item, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync($"{PatchQuery}", WithModified(item));
    }

    public async Task<bool> Delete(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync($"{DeleteQuery}", new DeleteParameters(id, Now));
    }

    public async Task<bool> Undelete(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync($"{UndeleteQuery}", new UndeleteParameters(id));
    }

    #endregion
}
