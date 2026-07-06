using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Api.Models;

internal sealed record CollectionResponse<T>(List<T> Items) : Timestamped where T : Timestamped
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public override DateTime Modified { get; init; } = HashModified(Items);

    private static DateTime HashModified(List<T> items)
    {
        // FNV-1a over 64-bit words: deterministic (no random seed), zero-allocation.
        // Detects additions, deletions and modifications — unlike Max() which misses deletions.
        var hash = 0xCBF29CE484222325UL; // FNV-1a 64-bit offset basis
        foreach (ref readonly T item in CollectionsMarshal.AsSpan(items))
            hash = (hash ^ (ulong)item.Modified.Ticks) * 0x100000001B3UL; // FNV-1a 64-bit prime
        return new DateTime((uint)(hash >> 32 ^ hash), DateTimeKind.Utc);
    }
}
