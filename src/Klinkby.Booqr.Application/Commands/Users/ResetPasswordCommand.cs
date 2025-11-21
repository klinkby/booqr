using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record ResetPasswordRequest(
    [Required]
    [StringLength(0xff)]
    [RegularExpression(
        """
        ^(?(")(".+?(?<!\\)"@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$
        """, ErrorMessage = "Email is not valid"
    )]
    string Email,

    [property: JsonIgnore]
    string Authority);

public sealed partial class ResetPasswordCommand(
    IUserRepository userRepository,
    IExpiringQueryString expiringQueryString,
    ChannelWriter<Message> channelWriter,
    IOptions<PasswordSettings> passwordSettings,
    ILogger<ResetPasswordCommand> logger
) : ICommand<ResetPasswordRequest>
{
    private readonly LoggerMessages _log = new(logger);
    private readonly PasswordSettings _settings = passwordSettings.Value;

    public async Task Execute(ResetPasswordRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _log.ResetPassword(query.Email);
        User? user = await userRepository.GetByEmail(query.Email.Trim(), cancellation);
        if (user != null)
        {
            Message message = ComposeMessage(user, query.Authority);
            _log.Enqueue(message.Id);
            await channelWriter.WriteAsync(message, cancellation);
        }
        else
        {
            _log.UnknownUser(query.Email);
        }
    }

    private Message ComposeMessage(User user, string authority) =>
        EmbeddedResource.Properties_PasswordReset_handlebars.ComposeMessage(
            user.Email,
            StringResources.ResetPasswordSubject,
            new()
            {
                ["name"] = user.Name ?? user.Email,
                ["resetlink"] = authority
                                + _settings.ResetPath
                                + expiringQueryString.Create(
                                    TimeSpan.FromHours(_settings.ResetTimeoutHours),
                                    user.GetPasswordResetParameters()),
                ["expiryhours"] = _settings.ResetTimeoutHours.ToString(CultureInfo.InvariantCulture)
            });

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(200, LogLevel.Information, "Reset {Email} user's password")]
        public partial void ResetPassword(string email);

        [LoggerMessage(201, LogLevel.Information, "Enqueue reset password message {MessageId}")]
        public partial void Enqueue(Guid messageId);

        [LoggerMessage(202, LogLevel.Warning, "Unknown email {Email}")]
        public partial void UnknownUser(string email);
    }
}
