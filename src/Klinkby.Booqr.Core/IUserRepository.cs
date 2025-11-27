using System.Text.Json.Serialization;

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
/// <param name="Name">
///     The display name of the user, or <c>null</c> if not provided.
/// </param>
/// <param name="Phone">
///     The phone number of the user, or <c>null</c> if not provided.
/// </param>
public sealed record User(
    string Email,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    string? PasswordHash,
    string Role,
    string? Name,
    long? Phone) : Audit;

/// <summary>
///     Represents a partially defined user with minimal editable properties.
/// </summary>
/// <remarks>
///     This class derives from the <see cref="Audit" /> record and provides a minimal representation of a user.
///     It is primarily used for scenarios where only partial user updates are required, such as patch operations.
/// </remarks>
/// <param name="Id">
///     The unique identifier of the user, used for targeting updates.
/// </param>
/// <param name="Email">
///     The email address of the user, or <c>null</c> if not provided.
/// </param>
/// <param name="PasswordHash">
///     The hashed password of the user, or <c>null</c> if not provided.
/// </param>
/// <param name="Role">
///     The role assigned to the user, or <c>null</c> if not provided.
/// </param>
/// <param name="Name">
///     The display name of the user, or <c>null</c> if not provided.
/// </param>
/// <param name="Phone">
///     The phone number of the user, or <c>null</c> if not provided.
/// </param>
public sealed record PartialUser : Audit
{
    public PartialUser(int id) => Id = id;

    public string? Email { get; init; }
    public string? PasswordHash { get; init; }
    public string? Role { get; init; }
    public string? Name { get; init; }
    public long? Phone { get; init; }
}

/// <summary>
///     Defines the standard user roles available in the system.
/// </summary>
/// <remarks>
///     These constants represent the role values that can be assigned to users
///     to control access permissions and capabilities within the application.
/// </remarks>
public static class UserRole
{
    /// <summary>
    ///     Represents a customer user who can manage their own profile and bookings.
    /// </summary>
    public const string Customer = nameof(Customer);

    /// <summary>
    ///     Represents an employee user who provides services and manage customer calendars.
    /// </summary>
    public const string Employee = nameof(Employee);

    /// <summary>
    ///     Represents an administrator user with full system access.
    /// </summary>
    public const string Admin = nameof(Admin);
}

/// <summary>
///     Provides data access operations for <see cref="User"/> entities.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    ///     Retrieves a user by their email address.
    /// </summary>
    /// <param name="email">The email address of the user to retrieve.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the <see cref="User"/>
    ///     if found, otherwise <c>null</c>.
    /// </returns>
    Task<User?> GetByEmail(string email, CancellationToken cancellation = default);

    /// <summary>
    ///     Applies a partial update to a user entity using the specified <see cref="PartialUser"/> object.
    ///     Only the non-null properties of <paramref name="partialUser"/> will be updated.
    /// </summary>
    /// <param name="partialUser">
    ///     The <see cref="PartialUser"/> instance containing the user ID and the properties to update.
    ///     Properties with <c>null</c> values are ignored.
    /// </param>
    /// <param name="cancellation">
    ///     A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result is <c>true</c> if the patch succeeded;
    ///     <c>false</c> if the update failed due to an optimistic concurrency conflict.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="partialUser"/> is <c>null</c>.
    /// </exception>
    Task<bool> Patch(PartialUser partialUser, CancellationToken cancellation = default);
}
