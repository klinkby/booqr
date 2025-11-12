namespace Klinkby.Booqr.Application.Abstractions;

public interface IRequestMetadata
{
    DateTime? Version { get; }
    string? TraceId { get; }
}
