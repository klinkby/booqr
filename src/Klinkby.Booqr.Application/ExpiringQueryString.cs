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
    bool TryParse(string queryString, [NotNullWhen(true)] out NameValueCollection? parsedParameters);
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

    public bool TryParse(string queryString, [NotNullWhen(true)] out NameValueCollection? parsedParameters)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(queryString);
        parsedParameters = null;

        if (!DateTime.TryParse(
                parameters[ExpiresKey],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime expiresValue))
        {
            return false;
        }

        if (Now > expiresValue)
        {
            return false;
        }

        var index = queryString.IndexOf(HashKeyMatch, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var originalQuery = queryString[..index];

        var hashValue = parameters[HashKey];
        if (string.IsNullOrEmpty(hashValue))
        {
            return false;
        }

        var computedHash = HashAndEncodeToBase64Url(originalQuery);

        if (!string.Equals(computedHash, hashValue, StringComparison.Ordinal))
        {
            return false;
        }

        parsedParameters = parameters;
        return true;
    }

    private string HashAndEncodeToBase64Url(string text)
    {
        var hashBytes = HMACSHA3_384.HashData(Convert.FromBase64String(_hmacKey), Encoding.UTF8.GetBytes(text));
        var hashValue = Base64Url.EncodeToString(hashBytes);
        return hashValue;
    }
}
