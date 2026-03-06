namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("name", "duration")]
internal sealed partial class ServiceRepository(IConnectionProvider connectionProvider, TimeProvider timeProvider)
    : AuditRepository<Service>(timeProvider), IServiceRepository
{
    private const string TableName = "services";

    private const string GetWithEmployeesAllQuery =
        """
        SELECT s.id, s.name, s.duration, s.created, s.modified, s.deleted,
               array_remove(array_agg(es.employeeid), NULL) AS employees
        FROM services s
        LEFT JOIN employeeservices es ON es.serviceid = s.id
        WHERE s.deleted IS NULL
        GROUP BY s.id
        ORDER BY s.name
        LIMIT @Num OFFSET @Start
        """;

    private const string GetWithEmployeesByIdQuery =
        """
        SELECT s.id, s.name, s.duration, s.created, s.modified, s.deleted,
               array_remove(array_agg(es.employeeid), NULL) AS employees
        FROM services s
        LEFT JOIN employeeservices es ON es.serviceid = s.id
        WHERE s.deleted IS NULL AND s.id = @Id
        GROUP BY s.id
        """;

    #region IRepository

    public async IAsyncEnumerable<Service> GetAll(IPageQuery pageQuery,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<Service> query = connection.QueryUnbufferedAsync<Service>($"{GetWithEmployeesAllQuery}", pageQuery);
        await foreach (Service item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    public async Task<Service?> GetById(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QuerySingleOrDefaultAsync<Service>($"{GetWithEmployeesByIdQuery}", new GetByIdParameters(id));
    }

    public async Task<int> Add(Service newItem, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        var result = await connection.ExecuteScalarAsync($"{InsertQuery}", WithCreated(newItem));
        Debug.Assert(result is int);
        return (int)result;
    }

    public async Task<bool> Update(Service item, CancellationToken cancellation)
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
