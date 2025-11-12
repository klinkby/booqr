using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Activity = Klinkby.Booqr.Core.Activity;

namespace Klinkby.Booqr.Application;

public record struct ActivityQuery<TItem>(int UserId, int EntityId);

public interface IActivityRecorder
{
    // Generic
    void Add<TItem>(ActivityQuery<TItem> query);
    void Update<TItem>(ActivityQuery<TItem> query);
    void Delete<TItem>(ActivityQuery<TItem> query);
}

internal class ActivityRecorder(
    ChannelWriter<Activity> writer,
    TimeProvider timeProvider,
    IRequestMetadata? etagProvider = null
) : IActivityRecorder
{
    public void Add<TItem>(ActivityQuery<TItem> query)
    {
        Activity activity = CreateActivity(query);
        _ = writer.TryWrite(activity);
    }

    public void Update<TItem>(ActivityQuery<TItem> query)
    {
        Activity activity = CreateActivity(query);
        _ = writer.TryWrite(activity);
    }

    public void Delete<TItem>(ActivityQuery<TItem> query)
    {
        Activity activity = CreateActivity(query);
        _ = writer.TryWrite(activity);
    }

    private Activity CreateActivity<TItem>(ActivityQuery<TItem> query, [CallerMemberName] string action = "") =>
        new(0,
            timeProvider.GetUtcNow().UtcDateTime,
            etagProvider?.TraceId,
            query.UserId,
            typeof(TItem).Name,
            query.EntityId,
            action);
}
