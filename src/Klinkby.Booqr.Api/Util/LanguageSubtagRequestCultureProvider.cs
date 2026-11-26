using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Primitives;

namespace Klinkby.Booqr.Api.Util;

/// <summary>
///     <c>Accept-Language</c> provider that reduces every tag to its primary language subtag
///     (<c>da-DK</c> → <c>da</c>) before matching. The stock parent-culture fallback walks
///     <see cref="System.Globalization.CultureInfo.Parent" />, which under <c>InvariantGlobalization</c>
///     (the AOT image) is the invariant culture rather than <c>da</c>, so region tags would otherwise
///     silently fall back to the English default.
/// </summary>
internal sealed class LanguageSubtagRequestCultureProvider : AcceptLanguageHeaderRequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ProviderCultureResult? result = await base.DetermineProviderCultureResult(httpContext);
        if (result is null)
        {
            return null;
        }

        List<StringSegment> languages = result.UICultures
            .Select(static tag => tag.IndexOf('-') is var dash and > 0 ? tag.Subsegment(0, dash) : tag)
            .Distinct(StringSegmentComparer.OrdinalIgnoreCase)
            .ToList();
        return new ProviderCultureResult(languages);
    }
}
