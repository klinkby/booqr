namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a physical location with a name and optional address information.
/// </summary>
/// <remarks>
///     This record is immutable and inherits basic audit properties from the <see cref="Audit" /> base type.
///     Locations are used to specify where services can be booked.
/// </remarks>
/// <param name="Name">The name of the location.</param>
/// <param name="Address1">The first line of the address.</param>
/// <param name="Address2">The second line of the address.</param>
/// <param name="Zip">The postal code.</param>
/// <param name="City">The city name.</param>
public sealed record Location(
    string Name,
    string? Address1 = null,
    string? Address2 = null,
    string? Zip = null,
    string? City = null
) : Audit;

/// <summary>
///     Provides data access operations for <see cref="Location"/> entities.
/// </summary>
public interface ILocationRepository : IRepository<Location>;
