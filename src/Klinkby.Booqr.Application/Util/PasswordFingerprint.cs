using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Klinkby.Booqr.Application.Util;

/// <summary>
///     Keyed, domain-separated, truncated fingerprint of a password hash. Embedded in calendar feed links so
///     that a password change (new hash) revokes them, without exposing the hash.
/// </summary>
internal static class PasswordFingerprint
{
    private const string Domain = "calendar-feed-pv:";
    private const int Length = 15; // 120 bits

    internal static string Compute(string passwordHash, string base64HmacKey)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        var mac = HMACSHA256.HashData(
            Convert.FromBase64String(base64HmacKey),
            Encoding.UTF8.GetBytes(Domain + passwordHash));
        return Base64Url.EncodeToString(mac.AsSpan(0, Length));
    }

    internal static bool Verify(string? candidate, string passwordHash, string base64HmacKey)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(candidate),
            Encoding.ASCII.GetBytes(Compute(passwordHash, base64HmacKey)));
    }
}
