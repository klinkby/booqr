using Klinkby.Booqr.Application.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Klinkby.Booqr.Api.Util;

internal static class CommandExtensions
{
    internal const string RefreshTokenCookieName = "refresh_token";

    internal static async Task<Results<Ok<TResult>, BadRequest, NotFound>> GetSingle<TQuery, TResult>(
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

    internal static async Task<Results<Ok<OAuthTokenResponse>, UnauthorizedHttpResult, BadRequest>> GetAuthenticationTokenWithCookie<T>(
        this ICommand<T, Task<OAuthTokenResponse?>> command, T query, HttpContext context,
        CancellationToken cancellationToken) where T: notnull
    {
        OAuthTokenResponse? result = await command.Execute(query, cancellationToken);
        if (result is null)
        {
            return TypedResults.Unauthorized();
        }

        // Set refresh token in HttpOnly cookie
        context.Response.Cookies.Append(RefreshTokenCookieName, result.RefreshToken!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh",
            Expires = result.RefreshTokenExpiration
        });
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(result with { RefreshToken = string.Empty });
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

    internal static Task<Results<NoContent, Conflict, BadRequest>> NoContent<TQuery>(
        this ICommand<TQuery> command, TQuery query, ClaimsPrincipal user, CancellationToken cancellationToken)
        where TQuery : AuthenticatedRequest =>
        NoContent(command, query with { User = user }, cancellationToken);

    internal static async Task<Results<NoContent, UnauthorizedHttpResult, BadRequest>> NoContent(
        this ICommand<ChangePasswordRequest, Task<bool>> command, ChangePasswordRequest query,
        CancellationToken cancellationToken) =>
        await command.Execute(query, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Unauthorized();

    internal static async Task<Results<NoContent, Conflict, BadRequest>> NoContent<TQuery>(
        this ICommand<TQuery> command, TQuery query, CancellationToken cancellationToken)
        where TQuery : notnull
    {
        await command.Execute(query, cancellationToken);
        return TypedResults.NoContent();
    }
}
