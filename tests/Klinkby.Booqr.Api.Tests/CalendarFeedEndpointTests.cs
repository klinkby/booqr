using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Klinkby.Booqr.Api.Util;
using Klinkby.Booqr.Application;
using Klinkby.Booqr.Application.Commands.CalendarFeeds;
using Klinkby.Booqr.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     HTTP-boundary tests for the calendar feed. The link endpoint is JWT-protected; the feed endpoint
///     is anonymous (calendar apps cannot send a JWT) and answers a uniform 404 for any unusable link.
///     Feed contents need a live tenant database and are covered by the Application and Infrastructure
///     tests, so only the result mapping is exercised directly here.
/// </summary>
public class CalendarFeedEndpointTests
{
    private const string AliceHost = "alice.booqr.dk";
    private const int AliceTenantId = 42;
    private static readonly Tenant AliceTenant = new(AliceTenantId, "alice", "t_42", "Alice");

    [Fact]
    public async Task GIVEN_Anonymous_WHEN_GettingCalendarFeedLink_THEN_Unauthorized()
    {
        await using WebApiFixture fixture = new();
        using HttpClient client = fixture.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync(new Uri("/api/users/2/calendar-feed-link", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GIVEN_CustomerAskingForAnotherUser_WHEN_GettingCalendarFeedLink_THEN_Forbidden()
    {
        // Owner-only: the ownership check runs before any user lookup, so no database is reached.
        await using WebApiFixture fixture = new(allowedHosts: AliceHost,
            tenantRepository: new FakeTenantRepository(AliceTenant));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri("/api/users/3/calendar-feed-link", UriKind.Relative));
        request.Headers.Host = AliceHost;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            TestAuth.CreateToken(2, UserRole.Customer, AliceTenantId));

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GIVEN_Anonymous_WHEN_GettingFeedWithGarbageLink_THEN_NotFoundNotUnauthorized()
    {
        // Reachable without a JWT: the signed link is the only credential, and a bad one is a uniform 404.
        await using WebApiFixture fixture = new(allowedHosts: AliceHost,
            tenantRepository: new FakeTenantRepository(AliceTenant));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri("/api/calendar-feed.ics?garbage", UriKind.Relative));
        request.Headers.Host = AliceHost;

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("type", out _));
    }

    [Fact]
    public async Task GIVEN_AnonymousOnReservedHost_WHEN_GettingFeed_THEN_TenantNotFound()
    {
        await using WebApiFixture fixture = new(allowedHosts: "www.booqr.dk",
            tenantRepository: new FakeTenantRepository(null));
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri("/api/calendar-feed.ics?garbage", UriKind.Relative));
        request.Headers.Host = "www.booqr.dk";

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://www.booqr.dk/problems/tenant-not-found",
            body.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GIVEN_FeedResult_WHEN_MappingToCalendar_THEN_StreamsTextCalendarWithNoStore()
    {
        const string content = "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n";
        using var body = new MemoryStream();
        DefaultHttpContext context = new()
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = body }
        };
        string[] lines = ["BEGIN:VCALENDAR\r\n", "END:VCALENDAR\r\n"];
        Task<Result<CalendarFeed>> feed = Task.FromResult<Result<CalendarFeed>>(
            new CalendarFeed(lines.ToAsyncEnumerable()));

        Results<PushStreamHttpResult, ProblemHttpResult> mapped = await feed.ToCalendar(context);

        PushStreamHttpResult result = Assert.IsType<PushStreamHttpResult>(mapped.Result);
        Assert.Equal("text/calendar; charset=utf-8", result.ContentType);
        await result.ExecuteAsync(context);
        Assert.Equal(content, Encoding.UTF8.GetString(body.ToArray()));
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GIVEN_FeedFault_WHEN_MappingToCalendar_THEN_ProblemWithNoStore()
    {
        DefaultHttpContext context = new();
        Task<Result<CalendarFeed>> feed = Task.FromResult<Result<CalendarFeed>>(Problem.NotFound);

        Results<PushStreamHttpResult, ProblemHttpResult> mapped = await feed.ToCalendar(context);

        Assert.IsType<ProblemHttpResult>(mapped.Result);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }
}
