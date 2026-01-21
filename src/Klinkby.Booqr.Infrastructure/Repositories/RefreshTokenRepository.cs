namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("family", "userid", "expires", "created", "revoked", "replacedby")]
internal sealed partial class RefreshTokenRepository(IConnectionProvider connectionProvider) : IRefreshTokenRepository
{
    private const string TableName = "refreshtokens";

    public async Task<RefreshToken?> GetByHash(string hash, CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(
            $"SELECT hash,{CommaSeparated} FROM {TableName} WHERE hash=@Hash",
            new { Hash = hash});

    }

    public async Task Add(RefreshToken newItem, CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        await connection.ExecuteAsync(
            $"INSERT INTO {TableName} (hash,{CommaSeparated}) VALUES (@Hash,{ParametersCommaSeparated})",
            newItem);
    }

    public async Task<bool> RevokeSingle(string hash, DateTime timestamp, string? replacedBy, CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync(
            $"UPDATE {TableName} SET revoked=@Timestamp, replacedby=@ReplacedBy WHERE hash=@Hash AND revoked IS NULL AND replacedby IS NULL",
            new { Hash = hash, Timestamp = timestamp, ReplacedBy = replacedBy });
    }

    public async Task<int> RevokeAll(Guid family, DateTime timestamp, CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.ExecuteAsync(
            $"UPDATE {TableName} SET revoked=@Timestamp WHERE family=@Family AND revoked IS NULL AND replacedby IS NULL",
            new { Family=family, Timestamp=timestamp });
    }

    public async Task<int> Delete(DateTime before, CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.ExecuteAsync(
            $"DELETE FROM {TableName} WHERE expires<@Timestamp",
            new { Timestamp = before });
    }
}
