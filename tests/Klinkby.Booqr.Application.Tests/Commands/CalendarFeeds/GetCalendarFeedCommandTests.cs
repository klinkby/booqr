using System.Collections.Specialized;
using System.Globalization;
using Klinkby.Booqr.Application.Commands.CalendarFeeds;
using Klinkby.Booqr.Application.Util;
using Klinkby.Booqr.Core;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests.Commands.CalendarFeeds;

public class GetCalendarFeedCommandTests
{
    private const int TenantId = 7;
    private const int MaxEvents = 1000;
    private const string Host = "acme.booqr.dk";
    private static readonly string Key = NewHmacKey();
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private readonly FakeTimeProvider _time = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICalendarFeedRepository> _feed = new();

    public GetCalendarFeedCommandTests()
    {
        _feed.Setup(x => x.GetEvents(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Yield<CalendarFeedEvent>());
    }

    private static PasswordSettings Settings => new() { HmacKey = Key };

    private ExpiringQueryString Signer => new(Options.Create(Settings), _time);

    private GetCalendarFeedCommand CreateSut() => new(
        _users.Object,
        _feed.Object,
        Signer,
        Options.Create(Settings),
        _time,
        CreateTenantContext(TenantId),
        NullLogger<GetCalendarFeedCommand>.Instance);

    private static User WithBcrypt(User user, string password = "Passw0rd!") =>
        user with { PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password, 4), Deleted = null };

    private string Link(User user, int tenantId = TenantId) =>
        Signer.Create(Lifetime, user.GetCalendarFeedParameters(tenantId, Key));

    private void Returns(User user) =>
        _users.Setup(x => x.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    private static void AssertNotFound(Result<CalendarFeed> result)
    {
        var fault = Assert.IsType<Fault>(result.Value);
        Assert.Equal(Problem.NotFound.Type, fault.Problem.Type);
        Assert.True(string.IsNullOrEmpty(fault.Problem.Detail) || fault.Problem.Detail == Problem.NotFound.Detail);
    }

    private void AssertRepoNeverCalled() =>
        _feed.Verify(x => x.GetEvents(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

    /// <summary>Asserts success and enumerates the feed, which is when the events are read.</summary>
    private static async Task<string> Render(Result<CalendarFeed> result) =>
        string.Concat(await Assert.IsType<Success<CalendarFeed>>(result.Value).Value.Lines.ToListAsync());

    private async Task AssertRejected(string queryString)
    {
        AssertNotFound(await CreateSut().Execute(new GetCalendarFeedRequest(queryString, Host)));
        AssertRepoNeverCalled();
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidLink_WHEN_Execute_THEN_ReturnsIcalendarFeed(User generated, CalendarFeedEvent evt)
    {
        User user = WithBcrypt(generated);
        Returns(user);
        _feed.Setup(x => x.GetEvents(user.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(evt));
        _time.SetUtcNow(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));

        Result<CalendarFeed> result = await CreateSut().Execute(new GetCalendarFeedRequest(Link(user), Host));

        string content = await Render(result);
        Assert.StartsWith("BEGIN:VCALENDAR", content, StringComparison.Ordinal);
        Assert.Contains($"X-WR-CALNAME:{TestHelpers.TenantName}\r\n", content, StringComparison.Ordinal);
        Assert.Contains("DTSTAMP:20261125T120000Z\r\n", content, StringComparison.Ordinal);
        _feed.Verify(x => x.GetEvents(user.Id,
            new DateTime(2026, 11, 22, 23, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 2, 25, 12, 0, 0, DateTimeKind.Utc),
            MaxEvents,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidLinkWithEvent_WHEN_Execute_THEN_ContentContainsEventUid(
        User generated, CalendarFeedEvent evt)
    {
        User user = WithBcrypt(generated);
        Returns(user);
        _feed.Setup(x => x.GetEvents(user.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Yield(evt));

        Result<CalendarFeed> result = await CreateSut().Execute(new GetCalendarFeedRequest(Link(user), Host));

        string content = await Render(result);
        Assert.Contains("BEGIN:VEVENT\r\n", content, StringComparison.Ordinal);
        Assert.Contains($"UID:booking-{evt.Id.ToString(CultureInfo.InvariantCulture)}@{Host}\r\n", content,
            StringComparison.Ordinal);
    }

    [Theory]
    // Monday, just after local midnight (Sunday 23:30Z = Monday 00:30 CET)
    [InlineData("2026-11-22T23:30:00Z", "2026-11-22T23:00:00Z")]
    // Monday midday
    [InlineData("2026-11-23T10:00:00Z", "2026-11-22T23:00:00Z")]
    // Sunday evening (still previous week)
    [InlineData("2026-11-22T22:30:00Z", "2026-11-15T23:00:00Z")]
    // Week containing the spring DST change (Sun 2026-03-29): Monday 03-23 is CET, Sunday after the change
    [InlineData("2026-03-29T12:00:00Z", "2026-03-22T23:00:00Z")]
    // Week after the spring change: Monday 03-30 is CEST
    [InlineData("2026-04-01T12:00:00Z", "2026-03-29T22:00:00Z")]
    // Week containing the autumn DST change (Sun 2026-10-25): Monday 10-19 is CEST
    [InlineData("2026-10-24T12:00:00Z", "2026-10-18T22:00:00Z")]
    // Week after the autumn change: Monday 10-26 is CET
    [InlineData("2026-10-28T12:00:00Z", "2026-10-25T23:00:00Z")]
    public async Task GIVEN_Now_WHEN_Execute_THEN_WindowStartsMondayMidnightCopenhagen(string now, string expectedFrom)
    {
        var nowUtc = DateTimeOffset.Parse(now, CultureInfo.InvariantCulture);
        _time.SetUtcNow(nowUtc);
        User user = WithBcrypt(new User("a@b.dk", null, UserRole.Customer, null, null) { Id = 5 });
        Returns(user);

        await Render(await CreateSut().Execute(new GetCalendarFeedRequest(Link(user), Host)));

        _feed.Verify(x => x.GetEvents(user.Id,
            DateTime.Parse(expectedFrom, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
            nowUtc.UtcDateTime.AddMonths(3),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_TamperedLink_WHEN_Execute_THEN_NotFound(User generated)
    {
        User user = WithBcrypt(generated);
        Returns(user);
        var link = Link(user).Replace($"?id={user.Id}&", $"?id={user.Id + 1}&", StringComparison.Ordinal);

        await AssertRejected(link);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ExpiredLink_WHEN_Execute_THEN_NotFound(User generated)
    {
        User user = WithBcrypt(generated);
        Returns(user);
        var link = Link(user);
        _time.Advance(Lifetime + TimeSpan.FromMinutes(1));

        await AssertRejected(link);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_MissingPv_WHEN_Execute_THEN_NotFound(User generated)
    {
        User user = WithBcrypt(generated);
        Returns(user);
        NameValueCollection parameters = user.GetCalendarFeedParameters(TenantId, Key);
        parameters.Remove(Query.PasswordVersion);

        await AssertRejected(Signer.Create(Lifetime, parameters));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_WrongAction_WHEN_Execute_THEN_NotFound(User generated, string action)
    {
        User user = WithBcrypt(generated);
        Returns(user);
        NameValueCollection parameters = user.GetCalendarFeedParameters(TenantId, Key);
        parameters[Query.Action] = action;

        await AssertRejected(Signer.Create(Lifetime, parameters));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_PasswordResetLink_WHEN_Execute_THEN_NotFound(User generated)
    {
        User user = WithBcrypt(generated);
        Returns(user);

        await AssertRejected(Signer.Create(Lifetime, user.GetPasswordResetParameters()));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_OtherTenantLink_WHEN_Execute_THEN_NotFoundAndRepoNotCalled(User generated)
    {
        User user = WithBcrypt(generated);
        Returns(user);

        await AssertRejected(Link(user, TenantId + 1));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_DeletedUser_WHEN_Execute_THEN_NotFound(User generated, DateTime t0)
    {
        User user = WithBcrypt(generated);
        var link = Link(user);
        Returns(user with { Deleted = t0 });

        await AssertRejected(link);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_UnknownUser_WHEN_Execute_THEN_NotFound(User generated)
    {
        User user = WithBcrypt(generated);
        _users.Setup(x => x.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await AssertRejected(Link(user));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_PasswordChanged_WHEN_Execute_THEN_OldLinkRevokedNewLinkAccepted(User generated)
    {
        User user = WithBcrypt(generated, "Passw0rd!");
        User current = user;
        _users.Setup(x => x.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => current);
        var oldLink = Link(user);
        GetCalendarFeedCommand sut = CreateSut();

        Assert.IsType<Success<CalendarFeed>>((await sut.Execute(new GetCalendarFeedRequest(oldLink, Host))).Value);

        current = WithBcrypt(user, "An0ther!Pass");
        AssertNotFound(await sut.Execute(new GetCalendarFeedRequest(oldLink, Host)));
        Assert.IsType<Success<CalendarFeed>>(
            (await sut.Execute(new GetCalendarFeedRequest(Link(current), Host))).Value);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ProfileEdited_WHEN_Execute_THEN_LinkStillAccepted(
        User generated, string name, string email, long phone)
    {
        User user = WithBcrypt(generated);
        User current = user;
        _users.Setup(x => x.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => current);
        var link = Link(user);

        current = user with { Name = name, Email = email, Phone = phone, Role = UserRole.Admin };

        Assert.IsType<Success<CalendarFeed>>((await CreateSut().Execute(new GetCalendarFeedRequest(link, Host))).Value);
    }
}
