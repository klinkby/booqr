namespace Klinkby.Booqr.Infrastructure.Repositories;

/// <summary>
///     Represents an abstract base repository that encapsulates common data access functionality for entities.
/// </summary>
/// <typeparam name="T">The type of the entity this repository handles. Must inherit from <see cref="Audit" />.</typeparam>
internal abstract class AuditRepository<T>(TimeProvider timeProvider)
    where T : Audit
{
    private DateTime? _utcNow;
    protected DateTime Now => _utcNow ??= timeProvider.GetUtcNow().UtcDateTime;

    protected T WithCreated(T item)
    {
        DateTime now = Now;
        if (item.Deleted is not null)
        {
            throw new InvalidOperationException("Cannot insert a new item that is already `deleted`");
        }

        return item with { Created = now, Modified = now };
    }

    protected T WithModified(T item)
    {
        return item with { Modified = Now };
    }
}
