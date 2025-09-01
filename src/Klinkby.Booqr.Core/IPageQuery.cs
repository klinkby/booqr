using System.ComponentModel.DataAnnotations;

namespace Klinkby.Booqr.Core;

public interface IPageQuery
{
    int? Start { get; }
    int? Num { get; }
}

public record struct PageQuery(
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100) : IPageQuery;
