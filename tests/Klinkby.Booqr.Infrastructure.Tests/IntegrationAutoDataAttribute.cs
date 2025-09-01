using System.Diagnostics.CodeAnalysis;
using AutoFixture;
using AutoFixture.Xunit2;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Infrastructure.Tests;

[SuppressMessage("Security", "CA5394:Do not use insecure randomness")]
internal sealed class IntegrationAutoDataAttribute : AutoDataAttribute
{
    public IntegrationAutoDataAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<DateTime>(c =>
            c.FromFactory(() =>
                DateTime.FromBinary(Random.Shared.NextInt64(DateTime.UnixEpoch.Ticks,
                    DateTime.UnixEpoch.AddYears(50).Ticks)).ToUniversalTime()));
        fixture.Customize<TimeSpan>(c =>
            c.FromFactory(() => TimeSpan.FromSeconds(Random.Shared.NextInt64(60, TimeSpan.SecondsPerDay))));

        // fixture.Customize<Service>(c => c
        //     .Without(p => p.Deleted));
        fixture.Customize<Location>(c => c
            .Without(p => p.Deleted));
        fixture.Customize<CalendarEvent>(c => c
            .Without(p => p.Deleted));
        fixture.Customize<User>(c => c
            .Without(p => p.Deleted));
        return fixture;
    }
}
