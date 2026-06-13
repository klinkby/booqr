using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Klinkby.Booqr.Core.Exceptions;

namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
///     Base class for authenticated requests with signed-in user information.
/// </summary>
/// <remarks>User property MUST be set POST validation</remarks>
public abstract record AuthenticatedRequest
{
    /// <summary>JWT registered claim name for the subject (user id), per RFC 7519.</summary>
    private const string SubClaimType = "sub";

    [JsonIgnore] public ClaimsPrincipal? User { get; init; }

    [JsonIgnore]
    public int AuthenticatedUserId
    {
        get
        {
            // Read the identity explicitly rather than relying on the JwtBearer handler's
            // default sub->NameIdentifier inbound mapping. Guard at runtime so a missing or
            // malformed claim fails closed instead of throwing FormatException (500) in Release.
            var nameIdValue = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User?.FindFirst(SubClaimType)?.Value;
            if (!int.TryParse(nameIdValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
            {
                throw new InvalidClaimException("Authenticated user identity claim is missing or invalid.");
            }

            return userId;
        }
    }

    [MemberNotNullWhen(true, nameof(User))]
    public bool IsOwnerOrEmployee(int ownerUserId)
    {
        ClaimsPrincipal? user = User;
        return user is not null
               && (user.IsInRole(UserRole.Employee) ||
                   user.IsInRole(UserRole.Admin) ||
                   ownerUserId == AuthenticatedUserId);
    }
    //
    // [MemberNotNullWhen(true, nameof(User))]
    // public bool IsOwnerOrAdmin(int ownerUserId)
    // {
    //     ClaimsPrincipal? user = User;
    //     return user is not null
    //            && (user.IsInRole(UserRole.Admin) ||
    //                ownerUserId == AuthenticatedUserId);
    // }
}
