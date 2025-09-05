namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private const string BaseUrl = "/api";

    public static void MapApi(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder baseRoute = app.MapGroup(BaseUrl);
        MapBookings(baseRoute);
        MapVacancies(baseRoute);
        MapLocations(baseRoute);
        MapServices(baseRoute);
        MapUsers(baseRoute);
    }
}
