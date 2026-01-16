using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.Auth;

public sealed partial class RefreshCommand(
    IUserRepository userRepository,
    IOAuth oauth,
    ILogger<RefreshCommand> logger) : ICommand<RefreshRequest, Task<OAuthTokenResponse?>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<OAuthTokenResponse?> Execute(RefreshRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.RefreshToken)) return null;

        var userId = await oauth.GetUserIdFromValidRefreshToken(query.RefreshToken, cancellation);
        if (userId is null) return null;

        User? user = await userRepository.GetById(userId.Value, cancellation);
        if (user is null) return null;

        OAuthTokenResponse response = await oauth.GenerateTokenResponse(user, cancellation);

        await oauth.InvalidateToken(query.RefreshToken, cancellation);
        return response;
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(290, LogLevel.Information, "User {Id} logged in")]
        public partial void LoggedIn(int id);
    }
}
