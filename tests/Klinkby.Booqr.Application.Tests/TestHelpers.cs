using System.Security.Claims;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests;

internal static class TestHelpers
{
    public static TimeProvider TimeProvider { get; } = new FakeTimeProvider();

    public static ClaimsPrincipal CreateUser(int id = 42, params string[] roles)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, $"{id}"));
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(identity);
    }

    public async static IAsyncEnumerable<T> Yield<T>(params T[] items)
    {
        foreach (T item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    // Convenience overload to avoid specifying generic type arguments in common tests
    public static IAsyncEnumerable<MyBooking> Yield(params MyBooking[] items)
    {
        return Yield<MyBooking>(items);
    }
}
