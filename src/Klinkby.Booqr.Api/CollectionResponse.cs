namespace Klinkby.Booqr.Api;

internal sealed record CollectionResponse<T>(IAsyncEnumerable<T> Items);
