namespace Klinkby.Booqr.Application.Commands.Auth;

public sealed record LogoutRequest : RefreshTokenDto;

public sealed class LogoutCommand(
    IOAuth oauth) : ICommand<LogoutRequest, Task<Result<bool>>>
{
    public async Task<Result<bool>> Execute(LogoutRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.RefreshToken))
        {
            return false;
        }

        await oauth.RevokeTokenFamily(query.RefreshToken, cancellation);
        return true;
    }
}
