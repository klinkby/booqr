using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Api;

/// <summary>
///     Central <see cref="ProblemDetailsOptions.CustomizeProblemDetails" /> hook. Every problem written through
///     <see cref="IProblemDetailsService" /> passes here — including the <c>AddValidation()</c> endpoint filter's
///     400s and binding failures surfaced by <c>UseStatusCodePages</c>, which otherwise reject the request before
///     any command runs and leave no trace in the log.
/// </summary>
internal static partial class ProblemDetailsCustomizer
{
    internal static void Customize(ProblemDetailsContext context)
    {
        HttpContext httpContext = context.HttpContext;
        ProblemDetails problem = context.ProblemDetails;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        if ((problem.Status ?? httpContext.Response.StatusCode) != StatusCodes.Status400BadRequest)
        {
            return;
        }

        LoggerMessages log = new(httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ProblemDetailsCustomizer)));
        HttpRequest request = httpContext.Request;
        // Field names and attribute messages only — never the submitted values, which may be PII.
        if (problem is HttpValidationProblemDetails { Errors.Count: > 0 } validation)
        {
            log.ValidationFailed(request.Method, request.Path,
                string.Join("; ", validation.Errors.Select(static e => $"{e.Key}: {string.Join(" ", e.Value)}")));
        }
        else
        {
            log.BadRequest(request.Method, request.Path, problem.Detail ?? problem.Title);
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Referenced by source generator")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(4, LogLevel.Warning, "Validation failed {Method} {Path}: {Errors}")]
        internal partial void ValidationFailed(string method, string path, string errors);

        [LoggerMessage(5, LogLevel.Warning, "Bad request {Method} {Path}: {Reason}")]
        internal partial void BadRequest(string method, string path, string? reason);
    }
}
