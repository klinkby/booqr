using System.Diagnostics;
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

    internal bool CanUserAccess(int resourceForUserId)
    {
        var userId = AuthenticatedUserId;
        if (userId == resourceForUserId)
        {
            return true; // is for me
        }

        var isEmployee = User!.IsInRole(UserRole.Employee) || User.IsInRole(UserRole.Admin);
        if (isEmployee)
        {
            return true; // I am employee
        }

        return false;
    }
}
