using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Klinkby.Booqr.Application;
using Klinkby.Booqr.Application.Commands.CalendarFeeds;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Klinkby.Booqr.Api.Util;

internal static class CommandExtensions
{
    internal const string RefreshTokenCookieName = "refresh_token";
    private const string CalendarContentType = "text/calendar; charset=utf-8";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

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
                Success<T> s => mapSuccess(s.Value),
                Fault e => e.Problem.ToProblemHttpResult()
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

    internal static ValueTask<Results<PushStreamHttpResult, ProblemHttpResult>> ToCalendar(
        this Task<Result<CalendarFeed>> commandResult, HttpContext context)
    {
        // Every outcome, the uniform 404 included, must not be cached by intermediaries or clients.
        context.SetNoStore();
        return MapResult(commandResult, feed => TypedResults.Stream(
            stream => WriteLines(stream, feed.Lines, context.RequestAborted), CalendarContentType));
    }

    /// <summary>Streams the lines as UTF-8 (no BOM) without buffering the whole document.</summary>
    private static async Task WriteLines(Stream stream, IAsyncEnumerable<string> lines, CancellationToken cancellation)
    {
        await using var writer = new StreamWriter(stream, Utf8NoBom, leaveOpen: true);
        await foreach (var line in lines.WithCancellation(cancellation))
        {
            await writer.WriteAsync(line.AsMemory(), cancellation);
        }

        await writer.FlushAsync(cancellation);
    }

    /// <summary>The single <see cref="Problem" /> → RFC 7807 HTTP result mapping.</summary>
    internal static ProblemHttpResult ToProblemHttpResult(this Problem p) =>
        TypedResults.Problem(p.Detail, null, p.HttpStatusCode, p.Title, p.Type);
}
