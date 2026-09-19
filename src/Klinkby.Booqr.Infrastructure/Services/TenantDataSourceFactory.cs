using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Services;

/// <summary>
///     Builds and caches, per tenant, an <see cref="NpgsqlDataSource" /> connected as the tenant's
///     PostgreSQL login role (<c>t_&lt;id&gt;</c>), per <c>docs/1-design.md</c> "Tenant data-source
///     factory" and "Connection pooling &amp; budget".
/// </summary>
/// <remarks>
///     <para>
///         <b>Bounded LRU with lease-refcounting.</b> Data sources are expensive (each owns its own
///         Npgsql connection pool), so this factory caches up to a configured maximum number of them,
///         evicting the least-recently-used entry only when it currently has <b>zero</b> in-flight
///         leases. A source with active leases is never disposed out from under its callers: eviction
///         walks the LRU from least- to most-recently-used and removes the first zero-lease entry it
///         finds; if every cached entry is currently leased, the cache is allowed to grow by one
///         rather than evict (and thus corrupt) a live connection pool.
///     </para>
///     <para>
///         <b>Connection budget.</b> Per docs/1-design.md "Connection pooling &amp; budget": Npgsql
///         pools are keyed by connection string (including user), so each <c>t_&lt;id&gt;</c> gets its
///         own pool; total server-side connections ≈
///         <c>replicas × active-tenants × MaxPoolSize</c>. Per-tenant pools are configured with a small
///         <c>Max Pool Size</c> (2–3) and <c>Min Pool Size=0</c> so idle tenants hold no connections.
///         The LRU bound additionally caps the number of *distinct tenant pools* alive at once
///         (independent of per-pool size), keeping the ceiling well below Postgres
///         <c>max_connections</c> even under high tenant churn.
///     </para>
///     <para>Registered as a singleton (see <see cref="TenantDataSourceServiceCollectionExtensions" />); thread-safe.</para>
/// </remarks>
internal sealed class TenantDataSourceFactory : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Entry> _entries = new();
    private readonly LinkedList<int> _lruOrder = new();
    private readonly string _baseConnectionString;
    private readonly string _masterSecret;
    private readonly int _maxPoolSize;
    private readonly int _maxEntries;
    private bool _disposed;

    internal TenantDataSourceFactory(string baseConnectionString, string masterSecret, int maxPoolSize, int maxEntries)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(masterSecret);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxPoolSize, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxEntries, 0);

        _baseConnectionString = baseConnectionString;
        _masterSecret = masterSecret;
        _maxPoolSize = maxPoolSize;
        _maxEntries = maxEntries;
    }

    /// <summary>
    ///     Acquires a lease on the (lazily built, cached) <see cref="NpgsqlDataSource" /> for
    ///     <paramref name="tenantId" />. Dispose the lease to release it; never dispose the underlying
    ///     <see cref="NpgsqlDataSource" /> directly.
    /// </summary>
    /// <remarks>
    ///     The connection's PostgreSQL login role is derived here as <c>t_&lt;id&gt;</c> from
    ///     <paramref name="tenantId" /> alone (matching <c>TenantProvisioner.TenantRole</c>), so the
    ///     cache key (the id) and the authenticated identity (the role + its id-derived password) are
    ///     the same single source of truth. The caller does not supply the role — that would let a
    ///     mismatched role/id pair authenticate under the wrong identity on a cache miss.
    /// </remarks>
    internal TenantDataSourceLease Acquire(int tenantId)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_entries.TryGetValue(tenantId, out Entry? existing))
            {
                existing.LeaseCount++;
                Touch(tenantId);
                return new TenantDataSourceLease(existing.DataSource, this, tenantId);
            }

            EvictIfNeededLocked();

            NpgsqlDataSource dataSource = BuildDataSource(tenantId);
            Entry entry = new(dataSource) { LeaseCount = 1 };
            _entries[tenantId] = entry;
            _lruOrder.AddFirst(tenantId);
            return new TenantDataSourceLease(dataSource, this, tenantId);
        }
    }

    /// <summary>Called by <see cref="TenantDataSourceLease.Dispose" />.</summary>
    internal void Release(int tenantId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(tenantId, out Entry? entry))
            {
                return;
            }

            entry.LeaseCount--;
            Debug.Assert(entry.LeaseCount >= 0, "Lease count went negative.");
        }
    }

    private NpgsqlDataSource BuildDataSource(int tenantId)
    {
        // Role and password both derive from tenantId (the cache key) — a single source of truth.
        // Keep in lock-step with TenantProvisioner.TenantRole ("t_<id>").
        var dbRole = $"t_{tenantId}";
        var password = TenantCredentials.DerivePassword(_masterSecret, tenantId);

        NpgsqlConnectionStringBuilder connectionStringBuilder = new(_baseConnectionString)
        {
            Username = dbRole,
            Password = password,
            MaxPoolSize = _maxPoolSize,
            MinPoolSize = 0,
        };

        NpgsqlSlimDataSourceBuilder builder = new(connectionStringBuilder.ConnectionString);
        builder.EnableArrays();
        return builder.Build();
    }

    /// <summary>Moves <paramref name="tenantId"/> to the most-recently-used position.</summary>
    private void Touch(int tenantId)
    {
        _lruOrder.Remove(tenantId);
        _lruOrder.AddFirst(tenantId);
    }

    /// <summary>
    ///     If the cache is at capacity, unlinks the least-recently-used entry with zero in-flight
    ///     leases from the LRU. Entries with active leases are skipped (never evicted while in use);
    ///     if every entry at capacity is in use, the cache is allowed to grow by one rather than evict
    ///     a live lease — bounded further only by the actual number of concurrently active tenants.
    /// </summary>
    private void EvictIfNeededLocked()
    {
        if (_entries.Count < _maxEntries)
        {
            return;
        }

        LinkedListNode<int>? node = _lruOrder.Last;
        while (node is not null)
        {
            LinkedListNode<int>? previous = node.Previous;
            var candidateId = node.Value;
            Entry candidate = _entries[candidateId];
            if (candidate.LeaseCount == 0)
            {
                _lruOrder.Remove(node);
                _entries.Remove(candidateId);
                candidate.DataSource.Dispose();
                return;
            }

            node = previous;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (Entry entry in _entries.Values)
            {
                entry.DataSource.Dispose();
            }

            _entries.Clear();
            _lruOrder.Clear();
        }
    }

    private sealed class Entry(NpgsqlDataSource dataSource)
    {
        internal NpgsqlDataSource DataSource { get; } = dataSource;
        internal int LeaseCount { get; set; }
    }
}

/// <summary>
///     A refcounted lease on a tenant's <see cref="NpgsqlDataSource" />. Dispose exactly once to
///     release the lease; disposing does not close or affect any connection already opened from the
///     data source. A reference type (rather than a struct) so it can be registered directly as a
///     scoped DI service (see
///     <see cref="TenantDataSourceServiceCollectionExtensions.AddTenantDataSources" />) and disposed
///     by the container when the scope ends.
/// </summary>
internal sealed class TenantDataSourceLease : IDisposable
{
    private readonly TenantDataSourceFactory _owner;
    private readonly int _tenantId;
    private bool _released;

    internal TenantDataSourceLease(NpgsqlDataSource dataSource, TenantDataSourceFactory owner, int tenantId)
    {
        DataSource = dataSource;
        _owner = owner;
        _tenantId = tenantId;
    }

    internal NpgsqlDataSource DataSource { get; }

    public void Dispose()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        _owner.Release(_tenantId);
    }
}
