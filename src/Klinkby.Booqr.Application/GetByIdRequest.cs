namespace Klinkby.Booqr.Application;

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

/// <summary>
/// Represents an authenticated request for a single integer id.
/// This request is used in scenarios where authentication is required
/// alongside the identification of a specific resource by its id.
/// </summary>
public sealed record AuthenticatedByIdRequest([Range(1, int.MaxValue)] int Id) : AuthenticatedRequest, IId
{
    public static implicit operator AuthenticatedByIdRequest(int id)
    {
        return FromInt32(id);
    }

    public static AuthenticatedByIdRequest FromInt32(int id)
    {
        return new AuthenticatedByIdRequest(id);
    }
}
