namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a bookable service with a name and duration.
/// </summary>
/// <remarks>
///     This record is immutable and inherits basic audit properties from the <see cref="Audit" /> base type.
///     Services define what can be booked by customers and how long each booking takes.
/// </remarks>
/// <param name="Name">The name of the service.</param>
/// <param name="Duration">The time duration required for the service.</param>
public sealed record Service(
    string Name,
    TimeSpan Duration) : Audit;

/// <summary>
///     Provides data access operations for <see cref="Service"/> entities.
/// </summary>
public interface IServiceRepository : IRepository<Service>;
