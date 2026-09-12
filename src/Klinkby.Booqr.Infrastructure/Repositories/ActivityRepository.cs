using System.Data.Common;
using Klinkby.Booqr.Core;
using Microsoft.Extensions.Logging;
using Activity = Klinkby.Booqr.Core.Activity;

namespace Klinkby.Booqr.Infrastructure.Repositories;

internal sealed partial class ActivityRepository(
    IConnectionProvider connectionProvider,
    IBatchScope batchScope,
    ILogger<ActivityRepository> logger) : IActivityRepository
{
    private readonly LoggerMessages _log = new(logger);

    // On a tenant connection tenant_id is NOT written: the column's
    // DEFAULT app.tenant_of(current_user) stamps it, and RLS WITH CHECK would reject any other
    // value. The cross-tenant background consumer runs as booqr_batch (BYPASSRLS), which maps to
    // no tenant via app.tenant_of(), so the DEFAULT would resolve to NULL - that path (IBatchScope
    // enabled) must write tenant_id explicitly from the queued Activity.TenantId instead.
    private const string SelectColumns = "timestamp,requestid,userid,entity,entityid,action,tenant_id as tenantid";
    private const string InsertColumns = "timestamp,requestid,userid,entity,entityid,action";
    private const string ParametersCommaSeparated = "@timestamp,@requestid,@userid,@entity,@entityid,@action";
    private const string InsertColumnsWithTenant = "timestamp,requestid,userid,entity,entityid,action,tenant_id";
    private const string ParametersCommaSeparatedWithTenant = "@timestamp,@requestid,@userid,@entity,@entityid,@action,@tenantid";
    private const string TableName = "activities";

    public async IAsyncEnumerable<Activity> GetRange(DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        [EnumeratorCancellation]CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<Activity> query = connection.QueryUnbufferedAsync<Activity>(
            $"""
             SELECT id,{SelectColumns}
             FROM {TableName}
             WHERE (timestamp BETWEEN @fromTime AND @toTime)
             ORDER BY timestamp,id
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
            $"SELECT id,{SelectColumns} FROM {TableName} ORDER BY timestamp,id LIMIT @Num OFFSET @Start",
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
            $"SELECT id,{SelectColumns} FROM {TableName} WHERE id=@id",
            new GetByLongIdParameters(id));
    }

    public async Task<long> Add(Activity newItem, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        object? result = batchScope.IsEnabled
            ? await connection.ExecuteScalarAsync(
                $"INSERT INTO {TableName} ({InsertColumnsWithTenant}) VALUES ({ParametersCommaSeparatedWithTenant}) RETURNING id",
                newItem)
            : await connection.ExecuteScalarAsync(
                $"INSERT INTO {TableName} ({InsertColumns}) VALUES ({ParametersCommaSeparated}) RETURNING id",
                newItem);
        Debug.Assert(result is long);
        return (long)result;
    }

    /// <inheritdoc />
    public async Task Record(Activity activity, CancellationToken cancellation = default)
    {
        try
        {
            _ = await Add(activity, cancellation);
        }
        catch (DbException ex)
        {
            // Best-effort: a failed audit write must never fault the surrounding use case.
            _log.RecordActivityFailed(ex, ex.Message);
        }
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        private readonly ILogger _logger = logger;

        [LoggerMessage(1061, LogLevel.Warning, "Error recording activity: {Message}")]
        public partial void RecordActivityFailed(Exception ex, string message);
    }
}
