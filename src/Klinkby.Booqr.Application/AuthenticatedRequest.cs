using System.Diagnostics;
using System.Security.Claims;

namespace Klinkby.Booqr.Application;

/// <summary>
///     Base class for authenticated requests with signed-in user information.
/// </summary>
/// <remarks>User property MUST be set POST validation</remarks>
public abstract record AuthenticatedRequest
{
    public ClaimsPrincipal? User { get; init; }

    public string UserName
    {
        get
        {
            Debug.Assert(User?.Identity?.Name is not null);
            return User.Identity.Name;
        }
    }
}
