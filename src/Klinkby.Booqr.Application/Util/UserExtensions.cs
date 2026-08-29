using System.Collections.Specialized;
using System.Globalization;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Klinkby.Booqr.Application.Util;

internal static class Query
{
    public const string Id = "id";
    public const string ETag = "etag";
    public const string Action = "action";
    public const string ChangePasswordAction = "change-password";
}

internal static class UserExtensions
{
    internal static PartialUser WithPasswordHash(this PartialUser user, string password) =>
        user with { PasswordHash = BCryptNet.EnhancedHashPassword(password) };

    internal static NameValueCollection GetPasswordResetParameters(this User user) =>
        new()
        {
            [Query.Id] = user.Id.ToString(CultureInfo.InvariantCulture),
            [Query.ETag] = user.ETag,
            [Query.Action] = Query.ChangePasswordAction,
        };

    internal static bool ValidateETagParameter(this Audit user, NameValueCollection parameters) =>
        parameters[Query.ETag] == user.ETag;

    extension(User user)
    {
        /// <summary>
        ///     True when the user is staff (<see cref="UserRole.Employee" /> or
        ///     <see cref="UserRole.Admin" />) rather than a <see cref="UserRole.Customer" />.
        /// </summary>
        internal bool IsStaff =>
            user.Role == UserRole.Admin || user.Role == UserRole.Employee;
    }
}
