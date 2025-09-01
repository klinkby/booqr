using System.Data;

namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a unit of work that provides methods for managing database transactions.
///     This interface allows controlling the lifetime and isolation level of a transaction,
///     as well as committing or rolling back the transaction.
/// </summary>
public interface ITransaction
{
    ValueTask Begin(CancellationToken cancellation = default);
    ValueTask Begin(IsolationLevel isolationLevel, CancellationToken cancellation = default);
    ValueTask Commit(CancellationToken cancellation = default);
    ValueTask Rollback(CancellationToken cancellation = default);
}
