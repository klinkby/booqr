namespace Klinkby.Booqr.Application.Commands.Auth;

public sealed record LogOffRequest(
    string? RefreshToken
);

public sealed class LogOffCommand(
    IOAuth oauth) : ICommand<LogOffRequest>
{
    public async Task Execute(LogOffRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.RefreshToken))
        {
            return;
        }

        await oauth.RevokeTokenFamily(query.RefreshToken, cancellation);
    }
}
