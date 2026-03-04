namespace Klinkby.Booqr.Infrastructure.Repositories;

internal sealed class JobClaimRepository(IConnectionProvider connectionProvider) : IJobClaim
{
    private const string TableName = "scheduled_job_executions";

    public async Task<bool> TryClaimAsync(string jobName, DateOnly executionDate, CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync(
            $"""
             INSERT INTO {TableName} (job_name, execution_date, claimed_at)
             VALUES (@JobName, @ExecutionDate, now())
             ON CONFLICT DO NOTHING
             """,
            new { JobName = jobName, ExecutionDate = executionDate });
    }

    public async Task<int> DeleteOldClaimsAsync(DateOnly before, CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.ExecuteAsync(
            $"DELETE FROM {TableName} WHERE execution_date < @Before",
            new { Before = before });
    }
}
