using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Api.Models;

internal static class CollectionResponse
{
    public static async Task<CollectionResponse<T>> FromStream<T>(
        IAsyncEnumerable<T> stream,
        CancellationToken cancellation) where T : Timestamped
    {
        var items = new List<T>();
        var maxModified = DateTime.MinValue;
        await foreach (var item in stream.WithCancellation(cancellation))
        {
            items.Add(item);
            if (item.Modified > maxModified)
                maxModified = item.Modified;
        }
        return new CollectionResponse<T>(items) { Modified = maxModified };
    }
}

internal sealed record CollectionResponse<T>(List<T> Items) : Timestamped where T : Timestamped
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public override DateTime Modified { get; init; }
}
