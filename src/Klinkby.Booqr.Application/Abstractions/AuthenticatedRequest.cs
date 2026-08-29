using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Klinkby.Booqr.Core.Exceptions;

namespace Klinkby.Booqr.Application.Abstractions;


/// <summary>
///     Middleware <see cref="AuthenticatedRequestEndPointFilter" /> inject User into request.
/// </summary>
public interface IAuthenticatedRequest
{
    void SetUser(ClaimsPrincipal user);
}

/// <summary>
///     Base class for authenticated requests with signed-in user information.
/// </summary>
/// <remarks>User property MUST be set POST validation</remarks>
public abstract record AuthenticatedRequest : IAuthenticatedRequest
{
    /// <summary>JWT registered claim name for the subject (user id), per RFC 7519.</summary>
    private const string SubClaimType = "sub";
    private static readonly ClaimsPrincipal DefaultClaimsPrincipal = new(new ClaimsIdentity());
    private ClaimsPrincipal _user = DefaultClaimsPrincipal;

    [JsonIgnore]
    public ClaimsPrincipal User
    {
        get => _user;
        init => _user = value;
    }

    [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "Callable by AuthenticatedRequestEndPointFilter only")]
    void IAuthenticatedRequest.SetUser(ClaimsPrincipal user)
    {
        _user = user;
    }

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

    /// <summary>
    ///     True when the authenticated user is staff (Employee or Admin) and therefore not
    ///     subject to the customer-scoped data-access restrictions.
    /// </summary>
    [MemberNotNullWhen(true, nameof(User))]
    public bool IsStaff
    {
        get
        {
            ClaimsPrincipal? user = User;
            return user is not null
                   && (user.IsInRole(UserRole.Employee) || user.IsInRole(UserRole.Admin));
        }
    }

    /// <summary>
    ///     True when the authenticated user is staff (see <see cref="IsEmployeeOrAdmin" />) or
    ///     is the owner of the resource identified by <paramref name="ownerUserId" />.
    /// </summary>
    [MemberNotNullWhen(true, nameof(User))]
    public bool IsOwner(int ownerUserId) =>
        (User is not null && ownerUserId == AuthenticatedUserId);

    public bool IsStaffOrOwner(int ownerUserId) => IsStaff || IsOwner(ownerUserId);
}
