using System.Data;

namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a unit of work that provides methods for managing database transactions.
///     This interface allows controlling the lifetime and isolation level of a transaction,
///     as well as committing or rolling back the transaction.
/// </summary>
public interface ITransaction
{
    /// <summary>
    ///     Begins a new transaction with the default isolation level.
    /// </summary>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous begin operation.</returns>
    ValueTask Begin(CancellationToken cancellation = default);

    /// <summary>
    ///     Begins a new transaction with the specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous begin operation.</returns>
    ValueTask Begin(IsolationLevel isolationLevel, CancellationToken cancellation = default);

    /// <summary>
    ///     Commits the current transaction, persisting all changes to the database.
    /// </summary>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    ValueTask Commit(CancellationToken cancellation = default);

    /// <summary>
    ///     Rolls back the current transaction, discarding all changes.
    /// </summary>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous rollback operation.</returns>
    ValueTask Rollback(CancellationToken cancellation = default);
}
