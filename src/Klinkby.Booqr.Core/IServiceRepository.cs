using System.ComponentModel.DataAnnotations;

namespace Klinkby.Booqr.Core;

public sealed record Service(
    [property: Required]
    [property: StringLength(0xff)]
    string Name
) : Audit;

public interface IServiceRepository : IRepository<Service>;
