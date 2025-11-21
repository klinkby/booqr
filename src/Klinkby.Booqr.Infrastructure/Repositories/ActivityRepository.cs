using Klinkby.Booqr.Infrastructure.Services;
using Activity = Klinkby.Booqr.Core.Activity;

namespace Klinkby.Booqr.Infrastructure.Repositories;

internal sealed class ActivityRepository(
    IConnectionProvider connectionProvider) : IActivityRepository
{
    private const string CommaSeparated = "timestamp,requestid,userid,entity,entityid,action";
    private const string ParametersCommaSeparated = "@timestamp,@requestid,@userid,@entity,@entityid,@action";
    private const string TableName = "activities";

    public async IAsyncEnumerable<Activity> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        [EnumeratorCancellation]CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<Activity> query = connection.QueryUnbufferedAsync<Activity>(
            $"""
             SELECT id,{CommaSeparated}
             FROM {TableName}
             WHERE (timestamp BETWEEN @fromTime AND @toTime)
             LIMIT @Num OFFSET @Start
             """, new { fromTime, toTime, pageQuery.Start, pageQuery.Num });
        await foreach (Activity item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<Activity> GetAll(IPageQuery pageQuery,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<Activity> query = connection.QueryUnbufferedAsync<Activity>(
            $"SELECT id,{CommaSeparated} FROM {TableName} LIMIT @Num OFFSET @Start",
            new { pageQuery.Start, pageQuery.Num });
        await foreach (Activity item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    public async Task<Activity?> GetById(long id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QuerySingleOrDefaultAsync<Activity>(
            $"SELECT id,{CommaSeparated} FROM {TableName} WHERE id=@id",
            new GetByLongIdParameters(id));
    }

    public async Task<long> Add(Activity newItem, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        var result = await connection.ExecuteScalarAsync(
            $"INSERT INTO {TableName} ({CommaSeparated}) VALUES ({ParametersCommaSeparated}) RETURNING id",
            newItem);
        Debug.Assert(result is long);
        return (long)result;
    }
}
