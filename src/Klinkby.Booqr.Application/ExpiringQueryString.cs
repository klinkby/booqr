using System.Buffers.Text;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application;

public interface IExpiringQueryString
{
    string Create(TimeSpan lifetime, NameValueCollection? parameters = null);
    bool TryParse(string queryString,
        [NotNullWhen(true)] out NameValueCollection? parsedParameters,
        out QueryStringValidation validationStatus);
}

internal sealed class ExpiringQueryString(
    IOptions<PasswordSettings> passwordSettings,
    TimeProvider? timeProvider = null) : IExpiringQueryString
{
    private const string ExpiresKey = "expires";
    private const string HashKey = "hash";
    private const string HashKeyMatch = "&" + HashKey + "=";

    private readonly string _hmacKey = passwordSettings.Value.HmacKey;

    private DateTime Now => (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public string Create(TimeSpan lifetime, NameValueCollection? parameters = null)
    {
        DateTime expiresAt = Now + lifetime;
        var expiresPart = ExpiresKey + "=" +
                          HttpUtility.UrlEncode(expiresAt.ToString("s", CultureInfo.InvariantCulture));
        var queryString = expiresPart;

        if (parameters is { Count: > 0 })
        {
            // ParseQueryString returns internal HttpValueCollection subclass with a ToString() override that encodes querystrings
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(string.Empty);
            queryParameters.Add(parameters);

            queryString = (queryParameters.ToString() ?? string.Empty) + "&" + queryString;
        }

        queryString = "?" + queryString;
        var hashPart = HashKey + "=" + HashAndEncodeToBase64Url(queryString);

        return queryString + "&" + hashPart;
    }

    public bool TryParse(string queryString,
        [NotNullWhen(true)] out NameValueCollection? parsedParameters,
        out QueryStringValidation validationStatus)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(queryString);
        parsedParameters = null;

        if (!DateTime.TryParse(
                parameters[ExpiresKey],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime expiresValue))
        {
            validationStatus = QueryStringValidation.DateNotParsed;
            return false;
        }

        if (Now > expiresValue)
        {
            validationStatus = QueryStringValidation.Expired;
            return false;
        }

        var index = queryString.IndexOf(HashKeyMatch, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            validationStatus = QueryStringValidation.HashMissing;
            return false;
        }

        var originalQuery = queryString[..index];

        var hashValue = parameters[HashKey];
        if (string.IsNullOrEmpty(hashValue))
        {
            validationStatus = QueryStringValidation.HashEmpty;
            return false;
        }

        var computedHash = HashAndEncodeToBase64Url(originalQuery);

        // Constant-time comparison so validation timing does not leak how much of
        // the MAC matched. Both operands are Base64Url (ASCII); FixedTimeEquals
        // safely returns false on length mismatch.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(computedHash),
                Encoding.ASCII.GetBytes(hashValue)))
        {
            validationStatus = QueryStringValidation.IntegrityFailed;
            return false;
        }

        parsedParameters = parameters;
        validationStatus = QueryStringValidation.Success;
        return true;
    }

    private string HashAndEncodeToBase64Url(string text)
    {
        // Sign the exact bytes, apart from percent-encoding case. Do NOT upper-case the whole
        // text: that would make the MAC case-insensitive, so two query strings differing only by
        // case would share a signature and integrity could be bypassed for any case-sensitive
        // value. Canonicalizing only the hex digits of %XY triplets keeps every value's case
        // bound while surviving RFC 3986 normalization in transit.
        var hashBytes = HMACSHA3_384.HashData(
            Convert.FromBase64String(_hmacKey),
            Encoding.UTF8.GetBytes(CanonicalizePercentEncoding(text)));
        var hashValue = Base64Url.EncodeToString(hashBytes);
        return hashValue;
    }

    /// <summary>
    ///     Upper-cases the two hex digits of every well-formed %XY triplet, leaving everything
    ///     else untouched.
    /// </summary>
    /// <remarks>
    ///     RFC 3986 §6.2.2.1 defines percent-encoding as case-insensitive and has normalizers
    ///     upper-case the triplets, while <see cref="HttpUtility.UrlEncode(string)" /> emits them
    ///     in lower case ("%3a"). Mail clients, link rewriters and front-ends that re-serialize
    ///     the query therefore hand back "%3A" for the very bytes we signed. Signing the
    ///     canonical form makes the MAC stable across that rewrite without weakening it:
    ///     <see cref="HttpUtility.ParseQueryString(string)" /> already decodes both spellings to
    ///     the same character, so they were never distinguishable to the consumer.
    /// </remarks>
    private static string CanonicalizePercentEncoding(string text)
    {
        if (!text.Contains('%', StringComparison.Ordinal))
        {
            return text;
        }

        return string.Create(text.Length, text, static (span, source) =>
        {
            source.CopyTo(span);
            for (var i = 0; i <= span.Length - 3; i++)
            {
                if (span[i] != '%'
                    || !char.IsAsciiHexDigit(span[i + 1])
                    || !char.IsAsciiHexDigit(span[i + 2]))
                {
                    continue;
                }

                span[i + 1] = char.ToUpperInvariant(span[i + 1]);
                span[i + 2] = char.ToUpperInvariant(span[i + 2]);
                // Skip past the triplet so an encoded percent ("%253a") only has its own
                // "25" normalized — the trailing "3a" is literal payload on both sides.
                i += 2;
            }
        });
    }
}

public enum QueryStringValidation
{
    Success,
    DateNotParsed,
    Expired,
    HashMissing,
    HashEmpty,
    IntegrityFailed
}
