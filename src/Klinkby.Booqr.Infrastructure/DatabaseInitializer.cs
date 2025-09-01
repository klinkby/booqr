namespace Klinkby.Booqr.Infrastructure;

/// <summary>
///     Create a brand spanking new tenant database
/// </summary>
public interface IDatabaseInitializer
{
    Task Initialize(CancellationToken cancellationToken);
}

internal sealed class DatabaseInitializer(IConnectionProvider connectionProvider) : IDatabaseInitializer
{
    public async Task Initialize(CancellationToken cancellationToken)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellationToken);
        using StreamReader sr = new(
            typeof(DatabaseInitializer)
                .Assembly
                .GetManifestResourceStream("Klinkby.Booqr.Infrastructure.DDL.sql")!);
        var initScript = await sr.ReadToEndAsync(cancellationToken);
        await connection.ExecuteScalarAsync(initScript);
    }
}
