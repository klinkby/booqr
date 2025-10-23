namespace Klinkby.Booqr.Application.Models;

public record struct PageQuery(
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100) : IPageQuery;
