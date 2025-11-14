using Klinkby.Booqr.Infrastructure.Services;

namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("name", "address1", "address2", "zip", "city")]
internal sealed partial class LocationRepository(IConnectionProvider connectionProvider, TimeProvider timeProvider)
    : AuditRepository<Location>(timeProvider), ILocationRepository
{
    private const string TableName = "locations";

    #region IRepository

    /// <inheritdoc />
    public async IAsyncEnumerable<Location> GetAll(IPageQuery pageQuery,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<Location> query = connection.QueryUnbufferedAsync<Location>($"{GetAllQuery}", pageQuery);
        await foreach (Location item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    public async Task<Location?> GetById(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QuerySingleOrDefaultAsync<Location>($"{GetByIdQuery}", new GetByIdParameters(id));
    }

    public async Task<int> Add(Location newItem, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        var result = await connection.ExecuteScalarAsync($"{InsertQuery}", WithCreated(newItem));
        Debug.Assert(result is int);
        return (int)result;
    }

    public async Task<bool> Update(Location item, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync($"{UpdateQuery}", WithModified(item));
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
