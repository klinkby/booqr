using System.Security.Claims;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Application.Tests;

public static class TestHelpers
{
    public static ClaimsPrincipal CreateUser(int id = 42, params string[] roles)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        return new ClaimsPrincipal(identity);
    }

    public static async IAsyncEnumerable<T> Yield<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    // Convenience overload to avoid specifying generic type arguments in common tests
    public static IAsyncEnumerable<MyBooking> Yield(params MyBooking[] items) => Yield<MyBooking>(items);
}
