using System.Security.Claims;
using AutoFixture;

namespace Klinkby.Booqr.Application.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class ApplicationAutoDataAttribute : AutoDataAttribute
{
    public ApplicationAutoDataAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<ClaimsPrincipal>(c =>
            c.FromFactory(GetTestUser));
        return fixture;
    }

    public static ClaimsPrincipal GetTestUser()
    {
        Claim[] claims = [new(ClaimTypes.NameIdentifier, "42")];
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
}
