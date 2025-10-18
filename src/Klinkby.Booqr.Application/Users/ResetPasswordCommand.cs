using System.Security.Cryptography;
using System.Threading.Channels;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Klinkby.Booqr.Application.Users;

public sealed record ResetPasswordRequest(
    [Required]
    [StringLength(0xff)]
    [RegularExpression(
        """
        ^(?(")(".+?(?<!\\)"@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$
        """, ErrorMessage = "Email is not valid"
    )]
    string Email);

public sealed partial class ResetPasswordCommand(
    IUserRepository userRepository,
    ChannelWriter<Message> channelWriter,
    ILogger<ResetPasswordCommand> logger
) : ICommand<ResetPasswordRequest>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task Execute(ResetPasswordRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _log.ResetPassword(query.Email);
        User? user = await userRepository.GetByEmail(query.Email.Trim(), cancellation);
        if (user != null)
        {
            var password = GenerateRandomPassword();
            await userRepository.Update(WithPasswordHash(user, password), cancellation);
            Message message = CreateMessage(query.Email, password, "Sign up");
            await channelWriter.WriteAsync(message, cancellation);
        }
    }

    internal static User WithPasswordHash(User user, string password) =>
        user with { PasswordHash = BCryptNet.EnhancedHashPassword(password) };

    internal static Message CreateMessage(string email, string password, string subject)
    {
        var message = Message.From(
            email,
            subject,
            $"Your password is {password}.");
        return message;
    }

    internal static string GenerateRandomPassword(int length = 10) =>
        RandomNumberGenerator.GetString(
            "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz!@#$%&*()+-=?",
            length);

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(200, LogLevel.Information, "Reset {Email} user's password")]
        public partial void ResetPassword(string email);
    }
}
