namespace Klinkby.Booqr.Application;

public record Problem(string Type, string Title, int HttpStatusCode, string? Detail = null)
{
    private const string Prefix = "https://www.booqr.dk/problems/";

    public static Problem NotFound { get; } =
        new(Prefix + "not-found", "Resource not found", 404);

    public static Problem ValidationFailed { get; } =
        new(Prefix + "validation-failed", "Request validation failed", 400);

    public static Problem Unauthorized { get; } =
        new(Prefix + "unauthorized", "Authentication failed", 401);

    public static Problem Forbidden { get; } =
        new(Prefix + "forbidden", "Access to this resource is forbidden", 403);

    public static Problem Conflict { get; } =
        new(Prefix + "conflict", "Resource conflict", 409);

    public static Problem MidAirCollision { get; } =
        new(Prefix + "mid-air-collision", "State changed during operation", 412);
}
