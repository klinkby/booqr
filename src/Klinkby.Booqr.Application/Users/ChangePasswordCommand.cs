using BCryptNet = BCrypt.Net.BCrypt;

namespace Klinkby.Booqr.Application.Users;

public sealed record ChangePasswordRequest(
    [Required]
    [StringLength(0x7f)]
    string OldPassword,
    [Required]
    [StringLength(0x7f)]
    [RegularExpression(
        """
        ^(?=(.*[0-9]))(?=.*[\!@#$%^&*()\\[\]{}\-_+=~`|:;"'<>,./?])(?=.*[a-z])(?=(.*[A-Z])).{8,}$
        """, ErrorMessage = "Password is too simple")]
    string NewPassword) : AuthenticatedRequest;

public partial class ChangePasswordCommand(
    IUserRepository userRepository,
    ILogger<ChangePasswordCommand> logger
) : ICommand<ChangePasswordRequest, Task<bool>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<bool> Execute(ChangePasswordRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _log.ChangePassword(query.AuthenticatedUserId);
        User? user = await userRepository.GetById(query.AuthenticatedUserId, cancellation);
        if (user is null || !BCryptNet.EnhancedVerify(query.OldPassword.Trim(), user.PasswordHash))
        {
            _log.WrongPassword(query.AuthenticatedUserId);
            return false;
        }

        await userRepository.Update(ResetPasswordCommand.WithPasswordHash(user, query.NewPassword.Trim()), cancellation);
        _log.Changed(user.Email);
        return true;
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(210, LogLevel.Information, "Change {UserId} password")]
        public partial void ChangePassword(int userId);

        [LoggerMessage(211, LogLevel.Warning, "User {UserId} typed the wrong password")]
        public partial void WrongPassword(int userId);

        [LoggerMessage(212, LogLevel.Information, "Password for {Email} successfully changed")]
        public partial void Changed(string email);
    }
}
