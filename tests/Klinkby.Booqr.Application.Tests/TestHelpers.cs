using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests;

internal static class TestHelpers
{
    public static FakeTimeProvider TimeProvider => new();

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

    public static ExpiringQueryString CreateExpiringQueryString(TimeProvider timeProvider)
    {
        return new ExpiringQueryString(
        Options
            .Create(new PasswordSettings
            {
                HmacKey = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(
                        HMACSHA3_384.HashSizeInBytes))
            }),
            timeProvider);
    }
}
