using Npgsql;
using NpgsqlTypes;

namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("employeeid", "serviceid")]
internal sealed partial class EmployeeServiceRepository(IConnectionProvider connectionProvider)
    : IEmployeeServiceRepository
{
    private const string TableName = "employeeservices";

    public async Task Assign(int serviceId, int[] employeeIds, CancellationToken cancellation)
    {
        var connection = (NpgsqlConnection)await connectionProvider.GetConnection(cancellation);
        await using NpgsqlCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            WITH new_employees AS (
              SELECT unnest(@employeeIds) AS employeeid
            ),
            removed AS (
              DELETE FROM {TableName} es
              WHERE es.serviceid = @serviceId
                AND NOT EXISTS (
                    SELECT 1 FROM new_employees ne
                    WHERE ne.employeeid = es.employeeid
                )
            )
            INSERT INTO {TableName} ({CommaSeparated})
            SELECT employeeid, @serviceId FROM new_employees
            ON CONFLICT DO NOTHING;
            """;
        cmd.Parameters.Add("serviceId", NpgsqlDbType.Integer).Value = serviceId;
        cmd.Parameters.Add("employeeIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = employeeIds.ToArray();
        await cmd.ExecuteNonQueryAsync(cancellation);
    }
}
