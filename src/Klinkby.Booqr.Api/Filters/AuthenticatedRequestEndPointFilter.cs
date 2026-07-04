using Klinkby.Booqr.Application.Abstractions;

namespace Klinkby.Booqr.Api.Filters;

/// <summary>
///     Intercepts endpoints whose bound parameter implements
///     <see cref="IAuthenticatedRequest" />, injecting the authenticated user before the handler runs.
/// </summary>
internal sealed class AuthenticatedRequestEndPointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        IList<object?> arguments = context.Arguments;
        for (var i = arguments.Count - 1; i >= 0; i--)
        {
            if (arguments[i] is not IAuthenticatedRequest request)
            {
                continue;
            }

            request.SetUser(context.HttpContext.User);
            break;
        }

        return next(context);
    }
}
