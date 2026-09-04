namespace Klinkby.Booqr.Application.Commands.Auth;

public sealed record RefreshRequest : RefreshTokenDto;

public sealed class RefreshCommand(
    IUserRepository userRepository,
    IOAuth oauth,
    ITransaction transaction) : ICommand<RefreshRequest, Task<Result<OAuthTokenResponse>>>
{
    public async Task<Result<OAuthTokenResponse>> Execute(RefreshRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.RefreshToken)) return Problem.Unauthorized;

        var token = await oauth.GetValidRefreshToken(query.RefreshToken, cancellation);
        if (token is null) return Problem.Unauthorized;

        User? user = await userRepository.GetById(token.Value.UserId, cancellation);
        if (user is null) return Problem.Unauthorized;

        await transaction.Begin(cancellation);
        try
        {
            // Preserve the family so reuse detection can revoke the whole rotation chain.
            (OAuthTokenResponse response, var newTokenHash) = await oauth.GenerateTokenResponse(user, token.Value.Family, cancellation);

            // A false result means the presented token was already revoked/replaced between
            // validation and here - a concurrent refresh or a replay. Revoke the family and fail.
            if (!await oauth.InvalidateToken(query.RefreshToken, newTokenHash, cancellation))
            {
                await oauth.RevokeTokenFamily(query.RefreshToken, cancellation);
                await transaction.Commit(cancellation);
                return Problem.Unauthorized;
            }

            await transaction.Commit(cancellation);
            return response;
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }
    }
}
