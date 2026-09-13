using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Klinkby.Booqr.Infrastructure.Services;

/// <summary>
///     Derives the PostgreSQL login password for a tenant role (<c>t_&lt;id&gt;</c>) from a shared
///     master secret, per the credential model in <c>docs/1-design.md</c> ("Credential model").
/// </summary>
/// <remarks>
///     <para>
///         <b>Contract (must match byte-for-byte across the API and the admin CLI's
///         <c>--provision</c>/<c>--rotate</c> commands, see <c>docs/2-implementation.md</c> Phase 5):</b>
///     </para>
///     <list type="number">
///         <item>
///             <description>
///                 Message = the tenant id rendered as its decimal, invariant-culture ASCII string
///                 (e.g. id <c>12</c> → UTF-8 bytes of <c>"12"</c>) — <i>not</i> the raw 4-byte
///                 integer representation. This choice is arbitrary but must be shared by every
///                 caller that derives or verifies a tenant password.
///             </description>
///         </item>
///         <item>
///             <description>MAC = HMAC-SHA384(key = UTF-8 bytes of <paramref name="masterSecret"/> parameter, message above).</description>
///         </item>
///         <item>
///             <description>
///                 Encoding = base64url per RFC 4648 §5: standard Base64 with <c>+</c>→<c>-</c>,
///                 <c>/</c>→<c>_</c>, and <c>=</c> padding stripped.
///             </description>
///         </item>
///     </list>
///     <para>
///         This is the ONLY implementation of the derivation algorithm; do not duplicate it — the
///         admin CLI (Phase 5) must call this same class.
///     </para>
///     <para>
///         Never log <paramref name="masterSecret"/> or the returned password.
///     </para>
/// </remarks>
public static class TenantCredentials
{
    /// <summary>
    ///     Derives the password for tenant role <c>t_&lt;<paramref name="tenantId"/>&gt;</c>.
    /// </summary>
    /// <param name="masterSecret">The shared HMAC key (UTF-8 encoded). Never logged.</param>
    /// <param name="tenantId">The tenant's immutable integer id.</param>
    /// <returns>The base64url-encoded (unpadded) HMAC-SHA384 digest. Never logged.</returns>
    public static string DerivePassword(string masterSecret, int tenantId)
    {
        ReadOnlySpan<char> message = tenantId.ToString(CultureInfo.InvariantCulture);
        Span<byte> messageBytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(message.Length)];
        var messageByteCount = Encoding.UTF8.GetBytes(message, messageBytes);

        Span<byte> hmac = stackalloc byte[HMACSHA384.HashSizeInBytes];
        var written = HMACSHA384.HashData(
            Encoding.UTF8.GetBytes(masterSecret),
            messageBytes[..messageByteCount],
            hmac);

        return Base64Url.EncodeToString(hmac[..written]);
    }
}
