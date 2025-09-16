namespace Klinkby.Booqr.Core;

public sealed record Service(
    string Name,
    TimeSpan Duration) : Audit;

public interface IServiceRepository : IRepository<Service>;
