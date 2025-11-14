using Microsoft.AspNetCore.Http.HttpResults;

namespace Klinkby.Booqr.Api.Util;

internal static class CommandExtensions
{
    internal static async Task<Results<Ok<TResult>, BadRequest, NotFound>> GetSingle<TQuery, TResult>(
        this ICommand<TQuery, Task<TResult?>> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : notnull
    {
        TResult? result = await command.Execute(query, cancellationToken);
        return result is not null
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();
    }

    internal static Task<Results<Ok<CollectionResponse<TResult>>, BadRequest>> GetCollection<TQuery, TResult>(
        this ICommand<TQuery, IAsyncEnumerable<TResult>> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : IPageQuery
    {
        IAsyncEnumerable<TResult> items = command.Execute(query, cancellationToken);
        CollectionResponse<TResult> response = new(items);
        return Task.FromResult<Results<Ok<CollectionResponse<TResult>>, BadRequest>>(TypedResults.Ok(response));
    }

    internal static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, BadRequest>> GetAuthenticationToken(
        this ICommand<LoginRequest, Task<LoginResponse?>> command, LoginRequest query,
        CancellationToken cancellationToken)
    {
        LoginResponse? result = await command.Execute(query, cancellationToken);
        return result is not null
            ? TypedResults.Ok(result)
            : TypedResults.Unauthorized();
    }

    internal static async Task<Results<Created<CreatedResponse>, BadRequest>> Created<TQuery>(
        this ICommand<TQuery, Task<int>> command, TQuery query, ClaimsPrincipal user, string resourceName,
        CancellationToken cancellationToken)
        where TQuery : AuthenticatedRequest
    {
        var newId = await command.Execute(query with { User = user }, cancellationToken);
        return TypedResults.Created(
            new Uri($"{resourceName}/{newId}", UriKind.Relative),
            new CreatedResponse(newId));
    }

    internal static async Task<Results<Created<CreatedResponse>, BadRequest>> CreatedAnonymous<TQuery>(
        this ICommand<TQuery, Task<int>> command, TQuery query, string resourceName,
        CancellationToken cancellationToken)
        where TQuery : notnull
    {
        var newId = await command.Execute(query, cancellationToken);
        return TypedResults.Created(
            new Uri($"{resourceName}/{newId}", UriKind.Relative),
            new CreatedResponse(newId));
    }

    internal static Task<Results<NoContent, BadRequest>> NoContent<TQuery>(
        this ICommand<TQuery> command, TQuery query, ClaimsPrincipal user, CancellationToken cancellationToken)
        where TQuery : AuthenticatedRequest
    {
        return NoContent(command, query with { User = user }, cancellationToken);
    }

    internal static async Task<Results<NoContent, UnauthorizedHttpResult, BadRequest>> NoContent(
        this ICommand<ChangePasswordRequest, Task<bool>> command, ChangePasswordRequest query,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await command.Execute(query with { User = user }, cancellationToken);
        return result
            ? TypedResults.NoContent()
            : TypedResults.Unauthorized();
    }

    internal static async Task<Results<NoContent, BadRequest>> NoContent<TQuery>(
        this ICommand<TQuery> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : notnull
    {
        await command.Execute(query, cancellationToken);
        return TypedResults.NoContent();
    }
}
