using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Api.Models;

internal static class CollectionResponse
{
    public static async Task<CollectionResponse<T>> FromStream<T>(
        IAsyncEnumerable<T> stream,
        CancellationToken cancellation) where T : Timestamped
    {
        List<T> items = await stream.ToListAsync(cancellation);
        return new CollectionResponse<T>(items);
    }
}

internal sealed record CollectionResponse<T>(List<T> Items) : Timestamped where T : Timestamped
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public override DateTime Modified { get; init; } = Items
        .Select(x => x.Modified)
        .Concat([DateTime.MinValue])
        .Max();
}
