using System.Diagnostics;

namespace Klinkby.Booqr.Application.Models;

public record Employee : Timestamped
{
    public Employee(User other) : base(other)
    {
        Debug.Assert(other != null, nameof(other) + " != null");
        Id = other.Id;
        Name = other.Name;
    }

    public int Id { get; init; }
    public string? Name { get; init; }
}
