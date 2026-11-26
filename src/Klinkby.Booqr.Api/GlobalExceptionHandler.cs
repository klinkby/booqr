using Klinkby.Booqr.Api.Properties;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace Klinkby.Booqr.Api;

/// <summary>
///     Converts any unhandled exception into an RFC 7807 Problem Details response, always emitting
///     <c>application/problem+json</c> regardless of the request's <c>Accept</c> header. The status
///     code is chosen by <see cref="StatusCode.FromException" />.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = StatusCode.FromException(exception);
        httpContext.Response.StatusCode = status;

        var localizedTitle = LocalizedTitle(status);
        if (localizedTitle is not null)
        {
            httpContext.SetContentLanguage();
        }

        await httpContext.WriteProblemAsync(
            new ProblemDetails { Status = status, Title = localizedTitle ?? ReasonPhrases.GetReasonPhrase(status) },
            cancellationToken);
        return true;
    }

    /// <summary>Titles for the statuses <see cref="StatusCode.FromException" /> can produce.</summary>
    private static string? LocalizedTitle(int status) => status switch
    {
        StatusCodes.Status400BadRequest => StringResources.Status400Title,
        StatusCodes.Status401Unauthorized => StringResources.Status401Title,
        StatusCodes.Status403Forbidden => StringResources.Status403Title,
        StatusCodes.Status409Conflict => StringResources.Status409Title,
        StatusCodes.Status500InternalServerError => StringResources.Status500Title,
        StatusCodes.Status501NotImplemented => StringResources.Status501Title,
        StatusCodes.Status502BadGateway => StringResources.Status502Title,
        StatusCodes.Status503ServiceUnavailable => StringResources.Status503Title,
        StatusCodes.Status504GatewayTimeout => StringResources.Status504Title,
        _ => null
    };
}
