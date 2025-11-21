using System;
using Microsoft.AspNetCore.Http;

namespace Klinkby.Booqr.Api.Util;

internal static class HttpContextExtensions
{
    internal static Uri GetContextAuthority(this HttpContext context) =>
        new(context.Request.Scheme + Uri.SchemeDelimiter + context.Request.Host.Value,
            UriKind.Absolute);
}
