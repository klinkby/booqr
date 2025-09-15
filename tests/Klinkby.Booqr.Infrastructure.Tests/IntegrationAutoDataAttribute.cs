using System.Diagnostics.CodeAnalysis;
using AutoFixture;
using AutoFixture.Xunit2;
using Klinkby.Booqr.Core;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Infrastructure.Tests;

[SuppressMessage("Security", "CA5394:Do not use insecure randomness")]
[AttributeUsage(AttributeTargets.Method)]
internal sealed class IntegrationAutoDataAttribute() : AutoDataAttribute(CreateFixture)
{
    private static IFixture CreateFixture()
    {
        DateTime origo = new FakeTimeProvider().GetUtcNow().UtcDateTime.Date;
        var fixture = new Fixture();
        fixture.Customize<DateTime>(c =>
            c.FromFactory(() => origo));
        fixture.Customize<TimeSpan>(c =>
            c.FromFactory(() => TimeSpan.FromHours(1)));
        fixture.Customize<Location>(c => c
            .With(p => p.Zip, () => "2301")
            .Without(p => p.Deleted));
        fixture.Customize<Booking>(c => c
            .Without(p => p.Deleted));
        fixture.Customize<CalendarEvent>(c => c
            .With(p => p.EndTime, () => origo + TimeSpan.FromHours(1))
            .Without(p => p.Deleted));
        fixture.Customize<User>(c => c
            .With(p => p.Role, () => UserRole.Customer)
            .Without(p => p.Deleted));
        fixture.Customize<Service>(c => c
            .Without(p => p.Deleted));
        return fixture;
    }
}
