using System.ComponentModel.DataAnnotations;

namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents a user with email, password hash, and role.
/// </summary>
/// <remarks>
///     This class is a sealed record derived from the <see cref="Audit" /> class.
///     It inherits audit properties such as creation, modification, and deletion timestamps.
/// </remarks>
/// <param name="Email">
///     The email address of the user. Acts as the primary identifier for login purposes.
/// </param>
/// <param name="PasswordHash">
///     The hashed password of the user. Stored securely and used for authentication.
/// </param>
/// <param name="Role">
///     The role assigned to the user. Determines the user's permissions within the system.
/// </param>
public sealed record User(
    [property: Required]
    [property: StringLength(0xff)]
    string Email,
    [property: Required]
    [property: StringLength(0xff)]
    string PasswordHash,
    [property: Required]
    [property: StringLength(20)]
    string Role,
    [property: StringLength(0xff)] string? Name,
    long? Phone) : Audit;

public static class UserRole
{
    public const string Customer = nameof(Customer);
    public const string Employee = nameof(Employee);
    public const string Admin = nameof(Admin);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmail(string email, CancellationToken cancellation);
}
