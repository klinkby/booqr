namespace Klinkby.Booqr.Core;

/// <summary>
///     Provides distributed coordination for scheduled jobs to prevent duplicate execution across multiple instances.
/// </summary>
public interface IJobClaim : IRepository
{
    /// <summary>
    ///     Attempts to claim exclusive execution rights for a scheduled job on the given date.
    ///     Only one instance across the cluster will receive <c>true</c> for the same
    ///     <paramref name="jobName"/> and <paramref name="executionDate"/> combination.
    /// </summary>
    /// <param name="jobName">A stable, unique identifier for the job (e.g. <c>"flush-tokens"</c>).</param>
    /// <param name="executionDate">The calendar date of the scheduled execution.</param>
    /// <param name="cancellation">Token to propagate cancellation.</param>
    /// <returns><c>true</c> if this instance claimed the execution slot; <c>false</c> if another instance already did.</returns>
    Task<bool> TryClaimAsync(string jobName, DateOnly executionDate, CancellationToken cancellation = default);

    /// <summary>
    ///     Deletes claim records whose <c>execution_date</c> is before <paramref name="before"/> to prevent
    ///     unbounded table growth.
    /// </summary>
    /// <param name="before">Rows with an execution date strictly earlier than this value are removed.</param>
    /// <param name="cancellation">Token to propagate cancellation.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteOldClaimsAsync(DateOnly before, CancellationToken cancellation = default);
}
