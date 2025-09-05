using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application;

/// <summary>
///     Base class for authenticated requests with signed-in user information.
/// </summary>
/// <remarks>User property MUST be set POST validation</remarks>
public abstract record AuthenticatedRequest
{
    [JsonIgnore]
    public ClaimsPrincipal? User { get; init; }

    [JsonIgnore]
    internal int AuthenticatedUserId
    {
        get
        {
            var nameIdValue = User?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            Debug.Assert(nameIdValue is not null);
            return int.Parse(nameIdValue, CultureInfo.InvariantCulture);
        }
    }
}
