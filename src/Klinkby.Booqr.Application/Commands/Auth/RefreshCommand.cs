namespace Klinkby.Booqr.Application.Commands.Auth;

public sealed partial class RefreshCommand(
    IUserRepository userRepository,
    IOAuth oauth,
    ITransaction transaction) : ICommand<RefreshRequest, Task<OAuthTokenResponse?>>
{
    public async Task<OAuthTokenResponse?> Execute(RefreshRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.RefreshToken)) return null;

        var userId = await oauth.GetUserIdFromValidRefreshToken(query.RefreshToken, cancellation);
        if (userId is null) return null;

        User? user = await userRepository.GetById(userId.Value, cancellation);
        if (user is null) return null;

        await transaction.Begin(cancellation);
        try
        {
            OAuthTokenResponse response = await oauth.GenerateTokenResponse(user, cancellation);
            await oauth.InvalidateToken(query.RefreshToken, cancellation);
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
