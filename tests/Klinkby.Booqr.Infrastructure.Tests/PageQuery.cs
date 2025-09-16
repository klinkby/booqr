using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Infrastructure.Tests;

internal sealed record PageQuery(int? Start = 0, int? Num = 1000) : IPageQuery;
