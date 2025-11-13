namespace Klinkby.Booqr.Application.Models;

/// <summary>
/// Represents a pagination query for retrieving a subset of results.
/// </summary>
/// <param name="Start">The zero-based starting index for pagination. Defaults to 0.</param>
/// <param name="Num">The maximum number of items to return. Must be between 1 and 1000. Defaults to 100.</param>
public record struct PageQuery(
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100) : IPageQuery;
