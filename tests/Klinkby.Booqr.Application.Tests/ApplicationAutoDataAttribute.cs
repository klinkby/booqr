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
            c.FromFactory(() =>
            {
                Claim[] claims = [new(ClaimTypes.Name, "TestUser")];
                var identity = new ClaimsIdentity(claims, "Test");
                return new ClaimsPrincipal(identity);
            }));
        return fixture;
    }
}
