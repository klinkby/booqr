namespace Klinkby.Booqr.Api.Models;

/// <summary>
///     Scoped service that provide the If-None-Match header value if it is convertible to DateTime.
/// </summary>
internal sealed class ETagProvider : IETagProvider
{
    public DateTime? Version { get; set; }
}
