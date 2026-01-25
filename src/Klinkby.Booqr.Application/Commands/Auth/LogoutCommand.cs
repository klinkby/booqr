namespace Klinkby.Booqr.Application.Commands.Auth;

public sealed record LogoutRequest : RefreshTokenDto;

public sealed class LogoutCommand(
    IOAuth oauth) : ICommand<LogoutRequest>
{
    public async Task Execute(LogoutRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.RefreshToken))
        {
            return;
        }

        await oauth.RevokeTokenFamily(query.RefreshToken, cancellation);
    }
}
