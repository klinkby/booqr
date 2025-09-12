using Klinkby.Booqr.Application.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Klinkby.Booqr.Api.Extensions;

internal static class CommandExtensions
{
    public async static Task<Results<Ok<TResult>, BadRequest, NotFound>> GetSingle<TQuery, TResult>(
        this ICommand<TQuery, Task<TResult?>> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : notnull
    {
        TResult? result = await command.Execute(query, cancellationToken);
        return result is not null
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();
    }

    public static Task<Results<Ok<CollectionResponse<TResult>>, BadRequest>> GetCollection<TQuery, TResult>(
        this ICommand<TQuery, IAsyncEnumerable<TResult>> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : IPageQuery
    {
        IAsyncEnumerable<TResult> items = command.Execute(query, cancellationToken);
        CollectionResponse<TResult> response = new(items);
        return Task.FromResult<Results<Ok<CollectionResponse<TResult>>, BadRequest>>(TypedResults.Ok(response));
    }

    public async static Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, BadRequest>> GetAuthenticationToken(
        this ICommand<LoginRequest, Task<LoginResponse?>> command, LoginRequest query,
        CancellationToken cancellationToken)
    {
        LoginResponse? result = await command.Execute(query, cancellationToken);
        return result is not null
            ? TypedResults.Ok(result)
            : TypedResults.Unauthorized();
    }

    public async static Task<Results<Created<CreatedResponse>, BadRequest>> Created<TQuery>(
        this ICommand<TQuery, Task<int>> command, TQuery query, ClaimsPrincipal user, string resourceName,
        CancellationToken cancellationToken)
        where TQuery : AuthenticatedRequest
    {
        var newId = await command.Execute(query with { User = user }, cancellationToken);
        return TypedResults.Created(
            new Uri($"{resourceName}/{newId}", UriKind.Relative),
            new CreatedResponse(newId));
    }

    public static Task<Results<NoContent, BadRequest>> NoContent<TQuery>(
        this ICommand<TQuery> command, TQuery query, ClaimsPrincipal user, CancellationToken cancellationToken)
        where TQuery : AuthenticatedRequest
    {
        return NoContent(command, query with { User = user }, cancellationToken);
    }

    async private static Task<Results<NoContent, BadRequest>> NoContent<TQuery>(
        this ICommand<TQuery> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : notnull
    {
        await command.Execute(query, cancellationToken);
        return TypedResults.NoContent();
    }
}

internal record struct CreatedResponse(int Id);
