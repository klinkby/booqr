using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Klinkby.Booqr.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Klinkby.Booqr.Api.Util;

internal static class CommandExtensions
{
    internal const string RefreshTokenCookieName = "refresh_token";

    extension<T>(Task<Result<T>> commandResult) where T : notnull
    {
        internal ValueTask<Results<Ok<U>, ProblemHttpResult>> ToOk<U>(Func<T, U> mapSuccess) =>
            commandResult.MapResult(x => TypedResults.Ok(mapSuccess(x)));

        internal ValueTask<Results<Ok<T>, ProblemHttpResult>> ToOk() =>
            ToOk<T, T>(commandResult, x => x);

        private async ValueTask<Results<U, ProblemHttpResult>> MapResult<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods |
                                        DynamicallyAccessedMemberTypes.NonPublicMethods)] U>(
            Func<T, U> mapSuccess) where U : IResult =>
            await commandResult switch
            {
                Result<T>.Success s => mapSuccess(s.Value),
                Result<T>.Fault e => MapFault(e.Problem),
                _ => throw new UnreachableException("Result<T> has only Success and Fault subtypes")
            };
    }

    internal static async ValueTask<Results<Ok<CollectionResponse<T>>, ProblemHttpResult>> ToOk<T>(this Task<List<T>> commandResult)
        where T : Timestamped =>
        TypedResults.Ok(new CollectionResponse<T>(await commandResult));

    internal static ValueTask<Results<Created<CreatedResponse>, ProblemHttpResult>> ToCreated(
        this Task<Result<int>> commandResult, string resourceName)
        => commandResult.MapResult(x => TypedResults.Created(new Uri($"{resourceName}/{x}", UriKind.Relative), new CreatedResponse(x)));

    internal static ValueTask<Results<NoContent, ProblemHttpResult>> ToNoContent(
        this Task<Result<bool>> commandResult) =>
        MapResult(commandResult, _ => TypedResults.NoContent());

    private static ProblemHttpResult MapFault(Problem p) =>
        TypedResults.Problem(p.Detail, null, p.HttpStatusCode, p.Title, p.Type);
}
