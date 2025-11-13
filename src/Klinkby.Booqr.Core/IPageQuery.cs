namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents pagination parameters for querying collections of data.
/// </summary>
/// <remarks>
///     This interface provides properties for specifying the starting position and number of items
///     to retrieve in a paginated query.
/// </remarks>
public interface IPageQuery
{
    /// <summary>
    ///     Gets the zero-based starting index for the query.
    /// </summary>
    /// <value>The starting position, or <c>null</c> to start from the beginning.</value>
    int? Start { get; }

    /// <summary>
    ///     Gets the maximum number of items to retrieve.
    /// </summary>
    /// <value>The maximum number of items, or <c>null</c> to retrieve all items.</value>
    int? Num { get; }
}
