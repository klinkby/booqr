namespace Klinkby.Booqr.Api.Models;

internal sealed class RequestMetadata : IRequestMetadata
{
    public DateTime? Version { get; set; }
    public string? TraceId { get; set; }
}
