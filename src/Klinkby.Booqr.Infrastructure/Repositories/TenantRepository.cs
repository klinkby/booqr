using Klinkby.Booqr.Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Repositories;

/// <summary>
///     Resolves tenants from the central registry (<c>public.tenants</c>).
/// </summary>
/// <remarks>
///     Uses the registry <see cref="NpgsqlDataSource" /> (role <c>booqr_registry</c>, SELECT-only),
///     injected as a keyed service (<see cref="RegistryServiceCollectionExtensions.RegistryDataSourceKey" />)
///     — never the scoped tenant <see cref="IConnectionProvider" />. This keeps host/slug resolution
///     independent of any tenant connection, per docs/1-design.md ("Registry data source ...
///     resolution never uses a tenant connection").
/// </remarks>
internal sealed class TenantRepository(
    [FromKeyedServices(RegistryServiceCollectionExtensions.RegistryDataSourceKey)] NpgsqlDataSource registryDataSource)
    : ITenantRepository
{
    private const string GetBySlugQuery =
        """
        SELECT id, slug, db_role as dbrole, display_name as displayname
        FROM public.tenants
        WHERE slug = @slug AND deleted IS NULL
        """;

    /// <inheritdoc />
    public async Task<Tenant?> GetBySlug(string slug, CancellationToken cancellation = default)
    {
        await using NpgsqlConnection connection = await registryDataSource.OpenConnectionAsync(cancellation);
        return await connection.QuerySingleOrDefaultAsync<Tenant>(
            $"{GetBySlugQuery}", new GetBySlugParameters(slug));
    }
}
