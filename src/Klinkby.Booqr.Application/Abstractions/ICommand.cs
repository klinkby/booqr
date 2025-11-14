namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
/// Represents a command that executes an asynchronous operation based on a command without returning a result.
/// </summary>
/// <typeparam name="TQuery">The type of the query parameter used to execute the command.</typeparam>
public interface ICommand<in TQuery> where TQuery : notnull
{
    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="query">The query containing the data needed to execute the command.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Execute(TQuery query, CancellationToken cancellation = default);
}

/// <summary>
/// Represents a command that executes an operation based on a query and returns a response.
/// </summary>
/// <typeparam name="TQuery">The type of the query parameter used to execute the command.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the command.</typeparam>
public interface ICommand<in TQuery, out TResponse> where TQuery : notnull
{
    /// <summary>
    /// Executes the command and returns a response.
    /// </summary>
    /// <param name="query">The query containing the data needed to execute the command.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>The response produced by executing the command.</returns>
    TResponse Execute(TQuery query, CancellationToken cancellation = default);
}
