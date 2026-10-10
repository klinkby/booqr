namespace Klinkby.Booqr.Infrastructure.Repositories;

internal sealed class TenantNameRepository(IConnectionProvider connectionProvider) : ITenantNameRepository
{
    private const string GetDisplayNamesQuery =
        """
        SELECT id, display_name as displayname
        FROM public.tenants
        WHERE deleted IS NULL
        """;

    public async Task<IReadOnlyDictionary<int, string>> GetDisplayNames(CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IEnumerable<TenantDisplayName> rows = await connection.QueryAsync<TenantDisplayName>($"{GetDisplayNamesQuery}");
        return rows.ToDictionary(x => x.Id, x => x.DisplayName);
    }

    private sealed record TenantDisplayName(int Id, string DisplayName);
}
