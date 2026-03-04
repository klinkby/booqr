namespace Klinkby.Booqr.Infrastructure.Repositories;

internal sealed class EmployeeServiceRepository(IConnectionProvider connectionProvider)
    : IEmployeeServiceRepository
{
    private const string TableName = "employeeservices";
    private const string ServiceFields = "s.id, s.name, s.duration, s.created, s.modified, s.deleted";

    public async IAsyncEnumerable<Service> GetByEmployeeId(int employeeId, IPageQuery pageQuery,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<Service> query = connection.QueryUnbufferedAsync<Service>(
            $"""
             SELECT {ServiceFields}
             FROM services s
             JOIN {TableName} es ON s.id = es.serviceid
             WHERE es.employeeid = @employeeId
               AND s.deleted IS NULL
             ORDER BY s.name
             LIMIT @Num OFFSET @Start
             """, new { employeeId, pageQuery.Start, pageQuery.Num });
        await foreach (Service item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    public async Task Add(int employeeId, int serviceId, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        await connection.ExecuteAsync(
            $"INSERT INTO {TableName} (employeeid, serviceid) VALUES (@employeeId, @serviceId)",
            new { employeeId, serviceId });
    }

    public async Task<bool> Delete(int employeeId, int serviceId, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync(
            $"DELETE FROM {TableName} WHERE employeeid = @employeeId AND serviceid = @serviceId",
            new { employeeId, serviceId });
    }
}
