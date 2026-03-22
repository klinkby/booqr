using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents the base class for entities that require audit information such as creation, modification, and deletion
///     timestamps.
/// </summary>
/// <remarks>
///     This class is intended to be used as a base record for entities that require tracking of audit information.
///     The audit data includes:
///     - Creation timestamp.
///     - Modification timestamp.
///     - Optional deletion timestamp.
/// </remarks>
public abstract record Audit : Timestamped, IId
{
    /// <summary>
    ///     Gets or initializes the unique identifier for the entity.
    /// </summary>
    /// <value>The unique integer identifier.</value>
    public int Id { get; init; }

    /// <summary>
    ///     Gets or initializes the date and time when the entity was created.
    /// </summary>
    /// <value>The creation timestamp.</value>
    public DateTime Created { get; init; }

    /// <summary>
    ///     Gets or initializes the date and time when the entity was soft-deleted.
    /// </summary>
    /// <value>The deletion timestamp, or <c>null</c> if the entity has not been deleted.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Deleted { get; init; }

    /// <summary>
    ///     Gets or initializes the version timestamp for optimistic concurrency control.
    /// </summary>
    /// <value>The version timestamp, or <c>null</c> if not set.</value>
    /// <remarks>
    ///     This property is not serialized to JSON and is used internally for database concurrency checks.
    /// </remarks>
    [JsonIgnore]
    public DateTime? Version { get; init; }
}
