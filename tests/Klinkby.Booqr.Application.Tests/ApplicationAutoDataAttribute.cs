using AutoFixture;

namespace Klinkby.Booqr.Application.Tests;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ApplicationAutoDataAttribute : AutoDataAttribute
{
    public ApplicationAutoDataAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture()
    {
        DateTime t0 = TestHelpers.TimeProvider.GetUtcNow().UtcDateTime;
        const int serviceId = 56;
        const int locationId = 101;
        const int vacancyId = 77;

        var fixture = new Fixture();
        fixture.Customize<TimeProvider>(c => c.FromFactory(() => TestHelpers.TimeProvider));
        fixture.Customize<DateTime>(c => c
            .FromFactory(() => t0));
        fixture.Customize<ClaimsPrincipal>(c => c
            .FromFactory(GetTestUser));
        fixture.Customize<Service>(c => c
            .With(p => p.Id, serviceId)
            .With(p => p.Duration, TimeSpan.FromHours(1)));
        fixture.Customize<Location>(c => c
            .With(p => p.Id, locationId));
        fixture.Customize<CalendarEvent>(c => c
            .With(p => p.Id, vacancyId)
            .With(p => p.StartTime, t0)
            .With(p => p.EndTime, t0.AddHours(1)));
        fixture.Customize<MyBooking>(c => c
            .With(p => p.StartTime, t0)
            .With(p => p.EndTime, t0.AddHours(1))
            .With(p => p.ServiceId, serviceId)
            .With(p => p.LocationId, locationId));
        fixture.Customize<Booking>(c => c
            .With(p => p.ServiceId, serviceId));
        fixture.Customize<AddBookingRequest>(c => c
            .With(p => p.ServiceId, serviceId)
            .With(p => p.StartTime, t0));
        fixture.Customize<JwtSettings>(c => c
            .With(p => p.Key, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")
            .With(p => p.Issuer, "test-issuer")
            .With(p => p.Audience, "test-audience")
            .Without(p => p.AccessExpires)
            .Without(p => p.RefreshExpires));

        return fixture;
    }

    public static ClaimsPrincipal GetTestUser()
    {
        Claim[] claims = [new(ClaimTypes.NameIdentifier, "42")];
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
}
