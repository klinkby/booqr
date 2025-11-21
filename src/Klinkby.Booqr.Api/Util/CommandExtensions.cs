using Microsoft.AspNetCore.Http.HttpResults;

namespace Klinkby.Booqr.Api.Util;

internal static class CommandExtensions
{
    async internal static Task<Results<Ok<TResult>, BadRequest, NotFound>> GetSingle<TQuery, TResult>(
        this ICommand<TQuery, Task<TResult?>> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : notnull =>
        await command.Execute(query, cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    internal static Task<Results<Ok<CollectionResponse<TResult>>, BadRequest>> GetCollection<TQuery, TResult>(
        this ICommand<TQuery, IAsyncEnumerable<TResult>> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : IPageQuery =>
        Task.FromResult<Results<Ok<CollectionResponse<TResult>>, BadRequest>>(
            TypedResults.Ok(new CollectionResponse<TResult>(command.Execute(query, cancellationToken))));

    async internal static Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, BadRequest>> GetAuthenticationToken(
        this ICommand<LoginRequest, Task<LoginResponse?>> command, LoginRequest query,
        CancellationToken cancellationToken) =>
        await command.Execute(query, cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.Unauthorized();

    async internal static Task<Results<Created<CreatedResponse>, BadRequest>> Created<TQuery>(
        this ICommand<TQuery, Task<int>> command, TQuery query, ClaimsPrincipal user, string resourceName,
        CancellationToken cancellationToken)
        where TQuery : AuthenticatedRequest
    {
        var newId = await command.Execute(query with { User = user }, cancellationToken);
        return TypedResults.Created(
            new Uri($"{resourceName}/{newId}", UriKind.Relative),
            new CreatedResponse(newId));
    }

    async internal static Task<Results<Created<CreatedResponse>, BadRequest>> CreatedAnonymous<TQuery>(
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
        where TQuery : AuthenticatedRequest =>
        NoContent(command, query with { User = user }, cancellationToken);

    async internal static Task<Results<NoContent, UnauthorizedHttpResult, BadRequest>> NoContent(
        this ICommand<ChangePasswordRequest, Task<bool>> command, ChangePasswordRequest query,
        CancellationToken cancellationToken) =>
        await command.Execute(query, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Unauthorized();

    async internal static Task<Results<NoContent, BadRequest>> NoContent<TQuery>(
        this ICommand<TQuery> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : notnull
    {
        await command.Execute(query, cancellationToken);
        return TypedResults.NoContent();
    }
}
