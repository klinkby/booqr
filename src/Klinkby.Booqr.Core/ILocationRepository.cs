namespace Klinkby.Booqr.Core;

public sealed record Location(
    string Name,
    string? Address1 = null,
    string? Address2 = null,
    string? Zip = null,
    string? City = null
) : Audit;

public interface ILocationRepository : IRepository<Location>;
