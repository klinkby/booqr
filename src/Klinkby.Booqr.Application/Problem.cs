namespace Klinkby.Booqr.Application;

public record Problem(string Type, string Title, int HttpStatusCode, string? Detail = null)
{
    /// <summary>Base URI of every booqr problem <see cref="Type" />; their title/detail text is localized.</summary>
    public const string Prefix = "https://www.booqr.dk/problems/";

    // Computed per access, not cached: the title is localized to the current request's UI culture.
    public static Problem NotFound =>
        new(Prefix + "not-found", StringResources.NotFoundTitle, 404);

    public static Problem ValidationFailed =>
        new(Prefix + "validation-failed", StringResources.ValidationFailedTitle, 400);

    public static Problem Unauthorized =>
        new(Prefix + "unauthorized", StringResources.UnauthorizedTitle, 401);

    public static Problem Forbidden =>
        new(Prefix + "forbidden", StringResources.ForbiddenTitle, 403);

    public static Problem CancellationDeadlinePassed =>
        new(Prefix + "cancellation-deadline-passed", StringResources.CancellationDeadlinePassedTitle, 403);

    public static Problem Conflict =>
        new(Prefix + "conflict", StringResources.ConflictTitle, 409);

    public static Problem TooManyRequests =>
        new(Prefix + "haste", StringResources.TooManyRequestsTitle, 429);

    public static Problem MidAirCollision =>
        new(Prefix + "mid-air-collision", StringResources.MidAirCollisionTitle, 412);
}
