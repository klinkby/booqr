namespace Klinkby.Booqr.Core;

/// <summary>
/// Represents a refresh token used for authentication and session management.
/// </summary>
/// <param name="Hash">The hash of the refresh token.</param>
/// <param name="Family">The family of refresh tokens associated with the same user's login.</param>
/// <param name="UserId">The unique identifier of the user associated with the refresh token.</param>
/// <param name="Expires">The date and time when the refresh token expires.</param>
/// <param name="Created">The date and time when the refresh token was created.</param>
/// <param name="Revoked">The date and time when the refresh token was revoked, or <c>null</c> if not revoked.</param>
/// <param name="ReplacedBy">The hash of the refresh token that replaced this one, or <c>null</c> if not replaced.</param>
public sealed record RefreshToken(
    string Hash,
    Guid Family,
    int UserId,
    DateTime Expires,
    DateTime Created,
    DateTime? Revoked = null,
    string? ReplacedBy = null
);

/// <summary>
/// Defines a contract for repository operations related to managing <see cref="RefreshToken"/> entities.
/// </summary>
public interface IRefreshTokenRepository : IRepository
{
    /// <summary>
    ///     Retrieves an item of type <see cref="RefreshToken"/> from the repository based on the specified identifier.
    /// </summary>
    /// <param name="hash">The unique identifier of the item to retrieve.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the item of type
    ///     <see cref="RefreshToken" /> if found, otherwise <c>null</c>.
    /// </returns>
    Task<RefreshToken?> GetByHash(string hash, CancellationToken cancellation = default);

    /// <summary>
    ///     Adds a new item of type <see cref="RefreshToken"/> to the repository, setting its Created field,
    ///     and returns the new unique identifier.
    /// </summary>
    /// <param name="newItem">The item to be added to the repository.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    Task Add(RefreshToken newItem, CancellationToken cancellation = default);

    /// <summary>
    ///     Revokes a single refresh token by setting its Revoked field to the specified timestamp.
    /// </summary>
    /// <param name="hash">The unique identifier of the item to revoke.</param>
    /// <param name="timestamp">The timestamp of the revocation.</param>
    /// <param name="replacedBy">The hash of the refresh token that replaced this one, or <c>null</c> if not replaced.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result is <c>true</c> if an item was
    ///     revoked, otherwise <c>false</c>.
    /// </returns>
    Task<bool> RevokeSingle(string hash, DateTime timestamp, string? replacedBy, CancellationToken cancellation = default);

    /// <summary>
    ///     Revokes all refresh tokens associated with the specified family by marking them with the provided timestamp.
    /// </summary>
    /// <param name="family">The identifier of the family to which the refresh tokens belong.</param>
    /// <param name="timestamp">The time at which the tokens are marked as revoked.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the number of items revoked.
    /// </returns>
    Task<int> RevokeAll(Guid family, DateTime timestamp, CancellationToken cancellation = default);

    /// <summary>
    /// Purges refresh tokens that have expired before the specified timestamp.
    /// </summary>
    /// <param name="before">The time at which expired tokens are deleted.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the number of items deleted.
    /// </returns>
    Task<int> Delete(DateTime before, CancellationToken cancellation = default);
}
