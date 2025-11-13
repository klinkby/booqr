namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents an entity with a unique integer identifier.
/// </summary>
/// <remarks>
///     This interface is used as a marker for entities that have a unique integer-based identifier.
///     It is commonly inherited by entity base classes such as <see cref="Audit"/>.
/// </remarks>
public interface IId
{
    /// <summary>
    ///     Gets the unique identifier for the entity.
    /// </summary>
    /// <value>The unique integer identifier.</value>
    public int Id { get; }
}
