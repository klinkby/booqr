using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
public abstract record Audit : IId
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    public DateTime Created { get; init; }
    public DateTime Modified { get; init; }

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Deleted { get; init; }
}
