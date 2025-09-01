using System.ComponentModel.DataAnnotations;

namespace Klinkby.Booqr.Core;

public sealed record Location(
    [property: Required]
    [property: StringLength(0xff)]
    string Name,
    [property: StringLength(0xff)] string? Address1 = null,
    [property: StringLength(0xff)] string? Address2 = null,
    [property: StringLength(20)] string? Zip = null,
    [property: StringLength(0xff)] string? City = null
) : Audit;

public interface ILocationRepository : IRepository<Location>;
