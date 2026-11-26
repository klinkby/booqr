using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Commands.CalendarFeeds;

public sealed record GetCalendarFeedLinkRequest([property: Range(1, int.MaxValue)] int Id) : AuthenticatedRequest
{
    /// <summary>Set by the endpoint from the request; not bindable from the query string.</summary>
    [JsonIgnore]
    public string Authority { get; init; } = "";
}

public sealed record CalendarFeedLink(Uri Url, DateTime Expires);

public sealed partial class GetCalendarFeedLinkCommand(
    IUserRepository userRepository,
    IExpiringQueryString expiringQueryString,
    IOptions<PasswordSettings> passwordSettings,
    TimeProvider timeProvider,
    ITenantContext tenantContext,
    ILogger<GetCalendarFeedLinkCommand> logger
) : ICommand<GetCalendarFeedLinkRequest, Task<Result<CalendarFeedLink>>>
{
    private const string FeedPath = "/api/calendar-feed.ics";
    private readonly LoggerMessages _log = new(logger);
    private readonly PasswordSettings _settings = passwordSettings.Value;

    public async Task<Result<CalendarFeedLink>> Execute(GetCalendarFeedLinkRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Owner only: staff may not obtain a link on behalf of another user.
        if (!query.IsOwner(query.Id))
        {
            _log.NotOwner(query.AuthenticatedUserId, query.Id);
            return Problem.Forbidden;
        }

        User? user = await userRepository.GetById(query.Id, cancellation);
        if (user is null || user.Deleted is not null)
        {
            _log.UserNotFound(query.Id);
            return Problem.NotFound with { Detail = Localized.Format(StringResources.UserNotFound, query.Id) };
        }

        if (user.PasswordHash is null)
        {
            _log.NoPassword(query.Id);
            return Problem.NotFound with { Detail = Localized.Format(StringResources.UserHasNoPassword, query.Id) };
        }

        TimeSpan lifetime = TimeSpan.FromDays(_settings.CalendarFeedTimeoutDays);
        // ExpiringQueryString signs the expiry with whole-second precision ("s" format); report the same.
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        DateTime expires = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond)) + lifetime;
        var url = new Uri(query.Authority + FeedPath + expiringQueryString.Create(
            lifetime,
            user.GetCalendarFeedParameters(tenantContext.TenantId, _settings.HmacKey)));
        return new CalendarFeedLink(url, expires);
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(320, LogLevel.Warning, "User {UserId} is not permitted to get {Id}'s calendar feed link")]
        public partial void NotOwner(int userId, int id);

        [LoggerMessage(321, LogLevel.Warning, "Calendar feed link: user {UserId} not found")]
        public partial void UserNotFound(int userId);

        [LoggerMessage(322, LogLevel.Warning, "Calendar feed link: user {UserId} has no password")]
        public partial void NoPassword(int userId);
    }
}
