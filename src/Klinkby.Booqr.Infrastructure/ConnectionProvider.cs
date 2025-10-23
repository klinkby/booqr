using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Infrastructure;

internal interface IConnectionProvider
{
    ValueTask<DbConnection> GetConnection(CancellationToken cancellation);
}

internal sealed class ConnectionProvider([FromKeyedServices(nameof(ConnectionProvider))] DbConnection connection)
    : IConnectionProvider, IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        Debug.Assert(connection.State != ConnectionState.Executing);
        Debug.Assert(connection.State != ConnectionState.Fetching);
        Debug.Assert(connection.State != ConnectionState.Connecting);

        if (connection.State != ConnectionState.Closed)
        {
            await connection.CloseAsync();
        }

        await connection.DisposeAsync();
    }

    public async ValueTask<DbConnection> GetConnection(CancellationToken cancellation)
    {
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
