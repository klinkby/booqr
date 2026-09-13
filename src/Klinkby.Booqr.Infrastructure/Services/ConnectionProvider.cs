using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Infrastructure.Services;

internal interface IConnectionProvider
{
    ValueTask<DbConnection> GetConnection(CancellationToken cancellation);
}

internal sealed class ConnectionProvider(IServiceProvider serviceProvider)
    : IConnectionProvider, IAsyncDisposable
{
    // Resolved lazily on first GetConnection() rather than injected, so building the DI graph for
    // an endpoint that never touches the database (e.g. a request that fails validation) does not
    // trigger tenant resolution. The keyed DbConnection factory reads ITenantContext and fails
    // closed when no tenant is resolved; deferring that to actual use keeps the guard correct
    // without turning non-DB requests into 500s.
    private DbConnection? _connection;

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        Debug.Assert(_connection.State != ConnectionState.Executing);
        Debug.Assert(_connection.State != ConnectionState.Fetching);
        Debug.Assert(_connection.State != ConnectionState.Connecting);

        if (_connection.State != ConnectionState.Closed)
        {
            await _connection.CloseAsync();
        }

        await _connection.DisposeAsync();
    }

    public async ValueTask<DbConnection> GetConnection(CancellationToken cancellation)
    {
        DbConnection connection = _connection ??=
            serviceProvider.GetRequiredKeyedService<DbConnection>(nameof(ConnectionProvider));

        Debug.Assert(connection.State != ConnectionState.Executing);
        Debug.Assert(connection.State != ConnectionState.Fetching);
        Debug.Assert(connection.State != ConnectionState.Connecting);

        if (connection.State == ConnectionState.Broken)
        {
            await connection.CloseAsync();
        }

        if (connection.State == ConnectionState.Closed)
        {
            await connection.OpenAsync(cancellation);
        }

        return connection;
    }
}
