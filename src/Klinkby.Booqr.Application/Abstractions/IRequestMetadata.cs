namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
/// Provides metadata about an HTTP request, including versioning and tracing information.
/// </summary>
public interface IRequestMetadata
{
    /// <summary>
    /// Gets the version timestamp of the request, used for optimistic concurrency control.
    /// </summary>
    DateTime? Version { get; }

    /// <summary>
    /// Gets the unique trace identifier for the request, used for tracing.
    /// </summary>
    string? TraceId { get; }
}
