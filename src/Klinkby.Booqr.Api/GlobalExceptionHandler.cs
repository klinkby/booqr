using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace Klinkby.Booqr.Api;

/// <summary>
///     Converts any unhandled exception into an RFC 7807 Problem Details response, always emitting
///     <c>application/problem+json</c> regardless of the request's <c>Accept</c> header. The status
///     code is chosen by <see cref="StatusCode.FromException" />.
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = StatusCode.FromException(exception);
        httpContext.Response.StatusCode = status;

        ProblemDetails problem = new()
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status)
        };

        // Common path: honours content negotiation and the central CustomizeProblemDetails (traceId) hook.
        if (await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem
            }))
        {
            return true;
        }

        // Fallback: the writer declined (e.g. Accept: text/html). Force problem+json AOT-safely,
        // re-adding the traceId extension the customization would otherwise have supplied.
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            AppJsonSerializerContext.Default.ProblemDetails,
            ProblemJsonContentType,
            cancellationToken);
        return true;
    }
}
