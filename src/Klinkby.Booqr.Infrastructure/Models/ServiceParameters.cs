namespace Klinkby.Booqr.Infrastructure.Models;

// Service minus Employees: Dapper.AOT binds an int[] member via SqlMapper.PackListParameters,
// which roots reflection-based Dapper and breaks the AOT publish (IL2104/IL3053).
internal readonly record struct ServiceParameters(
    int Id,
    string Name,
    TimeSpan Duration,
    string? Description,
    DateTime Created,
    DateTime Modified,
    DateTime? Deleted,
    DateTime? Version)
{
    public static ServiceParameters From(Service s) =>
        new(s.Id, s.Name, s.Duration, s.Description, s.Created, s.Modified, s.Deleted, s.Version);
}
