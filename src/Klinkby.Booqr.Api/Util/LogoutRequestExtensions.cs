using Klinkby.Booqr.Application.Models;

namespace Klinkby.Booqr.Api.Util;

internal static class LogoutRequestExtensions
{
    internal static T WithRefreshToken<T>(this T request, HttpContext context) where T: RefreshTokenDto
    {
        request.RefreshToken = context.Request.Cookies[CommandExtensions.RefreshTokenCookieName];
        return request;
    }
}
