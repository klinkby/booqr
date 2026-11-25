using System.Collections.Specialized;
using System.Globalization;
using Klinkby.Booqr.Application.Commands.CalendarFeeds;
using Klinkby.Booqr.Application.Util;
using Klinkby.Booqr.Core;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests.Commands.CalendarFeeds;

public class GetCalendarFeedLinkCommandTests
{
    private const string Authority = "https://acme.booqr.dk";
    private const int TenantId = 7;
    private static readonly string Key = NewHmacKey();

    private readonly FakeTimeProvider _time = new();
    private readonly Mock<IUserRepository> _users = new();

    private ExpiringQueryString Signer => new(Options.Create(new PasswordSettings { HmacKey = Key }), _time);

    private GetCalendarFeedLinkCommand CreateSut(int days) => new(
        _users.Object,
        Signer,
        Options.Create(new PasswordSettings { HmacKey = Key, CalendarFeedTimeoutDays = days }),
        _time,
        CreateTenantContext(TenantId),
        NullLogger<GetCalendarFeedLinkCommand>.Instance);

    private static User WithBcrypt(User user) =>
        user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Passw0rd!", 4), Deleted = null };

    private static GetCalendarFeedLinkRequest OwnerRequest(int id) => new(id)
    {
        User = CreateUser(id, UserRole.Customer), Authority = Authority
    };

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Owner_WHEN_Execute_THEN_ReturnsSignedFeedUrl(User generated, int days, DateTime t0)
    {
        User user = WithBcrypt(generated);
        _users.Setup(x => x.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        GetCalendarFeedLinkRequest request = OwnerRequest(user.Id);

        Result<CalendarFeedLink> result = await CreateSut(days).Execute(request);

        var link = Assert.IsType<Success<CalendarFeedLink>>(result.Value).Value;
        Assert.StartsWith(Authority + "/api/calendar-feed.ics?", link.Url.AbsoluteUri, StringComparison.Ordinal);
        Assert.True(Signer.TryParse(link.Url.Query, out NameValueCollection? parameters, out _));
        Assert.Equal(user.Id.ToString(CultureInfo.InvariantCulture), parameters["id"]);
        Assert.Equal(TenantId.ToString(CultureInfo.InvariantCulture), parameters["tenant"]);
        Assert.Equal("calendar-feed", parameters["action"]);
        Assert.True(PasswordFingerprint.Verify(parameters["pv"], user.PasswordHash!, Key));
        // Reported expiry equals the whole-second expiry signed into the link.
        Assert.Equal(t0.AddTicks(-(t0.Ticks % TimeSpan.TicksPerSecond)) + TimeSpan.FromDays(days), link.Expires);
        Assert.Equal(link.Expires.ToString("s", CultureInfo.InvariantCulture), parameters["expires"]);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Admin)]
    public async Task GIVEN_OtherUser_WHEN_Execute_THEN_ForbiddenAndNothingLoaded(string role)
    {
        var request = new GetCalendarFeedLinkRequest(99)
        {
            User = CreateUser(42, role), Authority = Authority
        };

        Result<CalendarFeedLink> result = await CreateSut(365).Execute(request);

        var fault = Assert.IsType<Fault>(result.Value);
        Assert.Equal(Problem.Forbidden.Type, fault.Problem.Type);
        _users.Verify(x => x.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NoPasswordHash_WHEN_Execute_THEN_Refused(User generated)
    {
        User user = generated with { PasswordHash = null, Deleted = null };
        _users.Setup(x => x.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        GetCalendarFeedLinkRequest request = OwnerRequest(user.Id);

        Result<CalendarFeedLink> result = await CreateSut(365).Execute(request);

        Assert.IsType<Fault>(result.Value);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_MissingUser_WHEN_Execute_THEN_NotFound(int id)
    {
        _users.Setup(x => x.GetById(id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        GetCalendarFeedLinkRequest request = OwnerRequest(id);

        Result<CalendarFeedLink> result = await CreateSut(365).Execute(request);

        var fault = Assert.IsType<Fault>(result.Value);
        Assert.Equal(Problem.NotFound.Type, fault.Problem.Type);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_Owner_WHEN_Execute_THEN_LinkDoesNotContainPasswordHash(User generated)
    {
        User user = WithBcrypt(generated);
        var hash = user.PasswordHash!;
        _users.Setup(x => x.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        GetCalendarFeedLinkRequest request = OwnerRequest(user.Id);

        Result<CalendarFeedLink> result = await CreateSut(365).Execute(request);

        var url = Assert.IsType<Success<CalendarFeedLink>>(result.Value).Value.Url.AbsoluteUri;
        Assert.DoesNotContain(hash, url, StringComparison.Ordinal);
        Assert.DoesNotContain(hash[7..29], url, StringComparison.Ordinal); // salt
        Assert.DoesNotContain(hash[29..], url, StringComparison.Ordinal); // digest
    }
}
