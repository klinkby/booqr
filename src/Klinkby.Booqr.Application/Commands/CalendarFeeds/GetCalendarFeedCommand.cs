using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Commands.CalendarFeeds;

public sealed record GetCalendarFeedRequest(string QueryString, string Host);

/// <summary>
///     A validated calendar feed: the VCALENDAR document as folded, CRLF-terminated content lines, produced
///     lazily while the events are read, so the document is never buffered whole. Enumerate once.
/// </summary>
public sealed record CalendarFeed(IAsyncEnumerable<string> Lines);

public sealed partial class GetCalendarFeedCommand(
    IUserRepository userRepository,
    ICalendarFeedRepository calendarFeedRepository,
    IExpiringQueryString expiringQueryString,
    IOptions<PasswordSettings> passwordSettings,
    TimeProvider timeProvider,
    ITenantContext tenantContext,
    ILogger<GetCalendarFeedCommand> logger
) : ICommand<GetCalendarFeedRequest, Task<Result<CalendarFeed>>>
{
    private const string DefaultCalendarName = "Booqr";
    private const int MaxEvents = 1000;
    private readonly LoggerMessages _log = new(logger);
    private readonly PasswordSettings _settings = passwordSettings.Value;

    public async Task<Result<CalendarFeed>> Execute(GetCalendarFeedRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!expiringQueryString.TryParse(
                query.QueryString,
                out NameValueCollection? parameters,
                out QueryStringValidation validation))
        {
            _log.InvalidQueryString(validation);
            return Rejected();
        }

        if (parameters[Query.Action] != Query.CalendarFeedAction)
        {
            _log.InvalidAction(parameters[Query.Action]);
            return Rejected();
        }

        if (!tenantContext.HasTenant
            || !int.TryParse(parameters[Query.Tenant], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var tenantId)
            || tenantId != tenantContext.TenantId)
        {
            _log.TenantMismatch();
            return Rejected();
        }

        if (!int.TryParse(parameters[Query.Id], NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            _log.UserIdNotAnInteger();
            return Rejected();
        }

        User? user = await userRepository.GetById(userId, cancellation);
        if (user is null || user.Deleted is not null)
        {
            _log.UserNotFound(userId);
            return Rejected();
        }

        if (user.PasswordHash is null
            || !PasswordFingerprint.Verify(parameters[Query.PasswordVersion], user.PasswordHash, _settings.HmacKey))
        {
            _log.Revoked(userId);
            return Rejected();
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        (DateTime from, DateTime to) = GetWindow(now);

        var calendarName = string.IsNullOrWhiteSpace(tenantContext.DisplayName)
            ? DefaultCalendarName
            : tenantContext.DisplayName;

        // The link is fully validated; the events are read and serialized only as the response is written.
        IAsyncEnumerable<CalendarFeedEvent> events = calendarFeedRepository.GetEvents(userId, from, to, MaxEvents, cancellation);
        return new CalendarFeed(CalendarSerializer.Lines(calendarName, query.Host, now,
            WarnIfTruncated(events, userId, cancellation), cancellation));
    }

    private async IAsyncEnumerable<CalendarFeedEvent> WarnIfTruncated(IAsyncEnumerable<CalendarFeedEvent> events,
        int userId, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var count = 0;
        await foreach (CalendarFeedEvent e in events.WithCancellation(cancellation))
        {
            count++;
            yield return e;
        }

        if (count >= MaxEvents)
        {
            _log.Truncated(userId, MaxEvents);
        }
    }

    private static Result<CalendarFeed> Rejected() => Problem.NotFound;

    /// <summary>Monday 00:00 (Europe/Copenhagen) of the current local week, until now + 3 months.</summary>
    private static (DateTime From, DateTime To) GetWindow(DateTime nowUtc)
    {
        DateOnly today = MailTime.LocalDate(nowUtc);
        DateOnly monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (MailTime.DayRangeUtc(monday).From, nowUtc.AddMonths(3));
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(330, LogLevel.Warning, "Calendar feed link invalid: {Validation}")]
        public partial void InvalidQueryString(QueryStringValidation validation);

        [LoggerMessage(331, LogLevel.Warning, "Calendar feed link has invalid action: {Action}")]
        public partial void InvalidAction(string? action);

        [LoggerMessage(332, LogLevel.Warning, "Calendar feed link is for another tenant")]
        public partial void TenantMismatch();

        [LoggerMessage(333, LogLevel.Warning, "Calendar feed link user id is not an integer")]
        public partial void UserIdNotAnInteger();

        [LoggerMessage(334, LogLevel.Warning, "Calendar feed link: user {UserId} not found or deleted")]
        public partial void UserNotFound(int userId);

        [LoggerMessage(335, LogLevel.Warning, "Calendar feed link for user {UserId} is revoked or has no password")]
        public partial void Revoked(int userId);

        [LoggerMessage(336, LogLevel.Warning, "Calendar feed for user {UserId} truncated to {MaxEvents} events")]
        public partial void Truncated(int userId, int maxEvents);
    }
}
