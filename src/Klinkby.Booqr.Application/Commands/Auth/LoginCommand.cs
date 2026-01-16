using System.Diagnostics.CodeAnalysis;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Klinkby.Booqr.Application.Commands.Auth;

public sealed record LoginRequest(
    [Required] [StringLength(0xff)] string Email,
    [Required] [StringLength(0xff)] string Password);

public sealed partial class LoginCommand(
    IUserRepository userRepository,
    IOAuth oauth,
    ILogger<LoginCommand> logger) : ICommand<LoginRequest, Task<OAuthTokenResponse?>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<OAuthTokenResponse?> Execute(LoginRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var userName = query.Email.Trim();

        User? user = await userRepository.GetByEmail(userName, cancellation);
        if (user is null)
        {
            _log.NotFound(userName);
            return null;
        }

        if (user.PasswordHash is null)
        {
            _log.NotConfirmed(user.Id);
            return null;
        }

        var isPasswordValid = BCryptNet.EnhancedVerify(query.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            _log.WrongPassword(user.Email);
            return null;
        }

        OAuthTokenResponse response = await oauth.GenerateTokenResponse(user, cancellation);

        _log.LoggedIn(user.Id);
        return response;
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(130, LogLevel.Information, "User {Id} logged in")]
        public partial void LoggedIn(int id);

        [LoggerMessage(131, LogLevel.Warning, "User {Email} not found")]
        public partial void NotFound(string email);

        [LoggerMessage(132, LogLevel.Warning, "User {Email} typed the wrong password")]
        public partial void WrongPassword(string email);

        [LoggerMessage(133, LogLevel.Warning, "User {Id} has not confirmed sign up")]
        public partial void NotConfirmed(int id);
    }
}
