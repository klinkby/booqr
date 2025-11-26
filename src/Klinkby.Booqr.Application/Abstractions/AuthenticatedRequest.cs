using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Abstractions;

/// <summary>
///     Base class for authenticated requests with signed-in user information.
/// </summary>
/// <remarks>User property MUST be set POST validation</remarks>
public abstract record AuthenticatedRequest
{
    [JsonIgnore] public ClaimsPrincipal? User { get; init; }

    [JsonIgnore]
    public int AuthenticatedUserId
    {
        get
        {
            var nameIdValue = User?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            Debug.Assert(nameIdValue is not null);
            return int.Parse(nameIdValue, CultureInfo.InvariantCulture);
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
