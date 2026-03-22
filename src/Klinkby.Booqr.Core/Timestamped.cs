using System.Globalization;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Core;

/// <summary>
///    Represents an entity that includes a modification timestamp for tracking the last update time.
/// </summary>
public record Timestamped {
    /// <summary>
    ///     Gets or initializes the date and time when the entity was last modified.
    /// </summary>
    /// <value>The last modification timestamp.</value>
    public virtual DateTime Modified { get; init; }

    /// <summary>
    ///     Gets the entity tag (ETag) for concurrency control, derived from the modification timestamp.
    /// </summary>
    /// <value>A string representation of the modification timestamp ticks in invariant culture format.</value>
    /// <remarks>
    ///     This property is used for optimistic concurrency control to detect mid-air collisions.
    ///     It is serialized as "_etag" in JSON format.
    /// </remarks>
    [JsonPropertyName("_etag"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ETag => Modified.Ticks.ToString(CultureInfo.InvariantCulture);
}
