using System;
using Microsoft.AspNetCore.Http;

namespace Klinkby.Booqr.Api.Util;

internal static class HttpContextExtensions
{
    internal static string GetContextAuthority(this HttpContext context) =>
        context.Request.Scheme + Uri.SchemeDelimiter + context.Request.Host.Value;
}
