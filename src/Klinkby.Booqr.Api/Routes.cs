namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    public static void MapApi(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder baseRoute = app.MapGroup("/api");
        MapVacancies(baseRoute);
        MapLocations(baseRoute);
        MapServices(baseRoute);
        MapUsers(baseRoute);
    }
}
