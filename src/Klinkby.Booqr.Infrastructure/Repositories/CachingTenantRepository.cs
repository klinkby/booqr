using System.Collections.Concurrent;
using Klinkby.Booqr.Infrastructure.Models;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Infrastructure.Repositories;

/// <summary>
///     Decorates <see cref="ITenantRepository" /> with a bounded, time-to-live in-memory cache keyed
///     by slug, so repeated host resolutions don't hit the registry database every request.
/// </summary>
/// <remarks>
///     Caches both positive and negative (not-found) results, so a deleted tenant stops resolving
///     once its cached entry expires (docs/1-design.md §5: "deleted tenant stops resolving within
///     the registry cache TTL"). Bounded by <see cref="RegistrySettings.CacheSize" />: when full, the
///     oldest entry (by insertion order) is evicted to make room. Thread-safe via
///     <see cref="ConcurrentDictionary{TKey,TValue}" />; hand-rolled rather than
///     <c>Microsoft.Extensions.Caching.Memory</c> because that package isn't referenced by this
///     project (see AGENTS.md: avoid new NuGet deps without noting it).
///     <para>
///     Wraps the plain <see cref="TenantRepository" /> concretely (not via <see cref="ITenantRepository" />,
///     to avoid resolving itself recursively). Because this class also implements
///     <see cref="ITenantRepository" />, the <c>AddRepositories</c> ServiceScan generator
///     (invoked by <c>AddInfrastructure</c>) auto-discovers <i>both</i> this type and
///     <see cref="TenantRepository" /> as <see cref="ITenantRepository" /> implementations.
///     <see cref="RegistryServiceCollectionExtensions.AddTenantRegistry" /> re-registers
///     <see cref="ITenantRepository" /> explicitly to resolve this decorator; Microsoft.DI resolves
///     a non-collection dependency using the <i>last</i> registration for the service type, so
///     calling <c>AddTenantRegistry</c> after <c>AddInfrastructure</c> makes this decorator the one
///     callers get from <c>GetRequiredService&lt;ITenantRepository&gt;()</c>.
///     </para>
/// </remarks>
internal sealed class CachingTenantRepository(
    TenantRepository inner,
    IOptions<RegistrySettings> options,
    TimeProvider timeProvider) : ITenantRepository
{
    private readonly ConcurrentDictionary<string, Entry> _cache = new();
    private readonly ConcurrentQueue<string> _insertionOrder = new();

    /// <inheritdoc />
    public async Task<Tenant?> GetBySlug(string slug, CancellationToken cancellation = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        if (_cache.TryGetValue(slug, out Entry cached) && cached.ExpiresAt > now)
        {
            return cached.Tenant;
        }

        Tenant? tenant = await inner.GetBySlug(slug, cancellation);

        RegistrySettings settings = options.Value;
        _cache[slug] = new Entry(tenant, now + settings.CacheTtl);
        _insertionOrder.Enqueue(slug);
        EvictOverflow(settings.CacheSize);

        return tenant;
    }

    private void EvictOverflow(int maxSize)
    {
        while (_cache.Count > maxSize && _insertionOrder.TryDequeue(out var oldestSlug))
        {
            _cache.TryRemove(oldestSlug, out _);
        }
    }

    private readonly record struct Entry(Tenant? Tenant, DateTimeOffset ExpiresAt);
}
