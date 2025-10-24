namespace Klinkby.Booqr.Api.Models;

internal sealed record CollectionResponse<T>(IAsyncEnumerable<T> Items);
