namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    public static void MapApi(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder baseRoute = app.MapGroup("/api");
        MapCalendar(baseRoute);
        MapLocation(baseRoute);
        MapService(baseRoute);
        MapUser(baseRoute);
    }
}
