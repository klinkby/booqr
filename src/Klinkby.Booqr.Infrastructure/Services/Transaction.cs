using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Klinkby.Booqr.Infrastructure.Services;

internal sealed partial class Transaction(IConnectionProvider connectionProvider, ILogger<Transaction> logger)
    : ITransaction, IAsyncDisposable
{
    private readonly LoggerMessages _log = new(logger);
    private IDisposable? _loggerScope;
    private DbTransaction? _transaction;

    public ValueTask DisposeAsync()
    {
        return Cleanup(true);
    }

    public async ValueTask Begin(CancellationToken cancellation)
    {
        await Begin(IsolationLevel.Unspecified, cancellation);
    }

    public async ValueTask Begin(IsolationLevel isolationLevel, CancellationToken cancellation)
    {
        if (_transaction is not null)
        {
            _log.TransactionAlreadyStarted();
            Debug.Fail("Transaction already started.");
        }

        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        _transaction = await connection.BeginTransactionAsync(isolationLevel, cancellation);
        _loggerScope = logger.BeginScope("Transaction begin");
    }

    public async ValueTask Commit(CancellationToken cancellation)
    {
        if (_transaction is null)
        {
            _log.NoTransaction();
            Debug.Fail("No transaction to commit.");
            throw new InvalidOperationException("No transaction to commit.");
        }

        _log.Commit();
        try
        {
            await _transaction.CommitAsync(cancellation);
        }
        catch
        {
            await Cleanup(false, cancellation);
            throw;
        }

        DbTransaction disposing = _transaction;
        _transaction = null;
        _loggerScope?.Dispose();
        await disposing.DisposeAsync();
    }

    public ValueTask Rollback(CancellationToken cancellation)
    {
        if (_transaction is null)
        {
            Debug.Fail("No transaction to rollback.");
            throw new InvalidOperationException("No transaction to rollback.");
        }

        return Cleanup(false, cancellation);
    }

    async private ValueTask Cleanup(bool fromDispose, CancellationToken cancellation = default)
    {
        if (_transaction is null)
        {
            return;
        }

        _log.Rollback(fromDispose);
        DbTransaction? disposing = _transaction;
        _transaction = null;
        _loggerScope?.Dispose();
        await using (disposing)
        {
            await disposing.RollbackAsync(cancellation);
        }
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(1000, LogLevel.Warning, "No transaction to commit")]
        public partial void NoTransaction();

        [LoggerMessage(1001, LogLevel.Warning, "Transaction already started")]
        public partial void TransactionAlreadyStarted();

        [LoggerMessage(1003, LogLevel.Information, "Commit")]
        public partial void Commit();

        [LoggerMessage(1004, LogLevel.Warning, "Rollback {FromDispose}")]
        public partial void Rollback(bool fromDispose);
    }
}
