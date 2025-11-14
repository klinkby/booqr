namespace Klinkby.Booqr.Application.Models;

/// <summary>
///     Anonymous requests for a single integer id.
/// </summary>
public record struct ByIdRequest([property: Range(1, int.MaxValue)] int Id) : IId
{
    public static implicit operator ByIdRequest(int id)
    {
        return FromInt32(id);
    }

    public static ByIdRequest FromInt32(int id)
    {
        return new ByIdRequest(id);
    }
}
