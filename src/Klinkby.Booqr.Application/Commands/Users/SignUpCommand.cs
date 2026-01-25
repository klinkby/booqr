using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record SignUpRequest(
    // https://gist.github.com/StephenWDickey/8cd8f97a36f357b0df8d2559b0e1c2ab
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

public sealed partial class SignUpCommand(
    IUserRepository userRepository,
    IExpiringQueryString expiringQueryString,
    ChannelWriter<Message> channelWriter,
    IActivityRecorder activityRecorder,
    IOptions<PasswordSettings> passwordSettings,
    ILogger<SignUpCommand> logger
) : ICommand<SignUpRequest, Task<int>>
{
    private readonly LoggerMessages _log = new(logger);
    private readonly PasswordSettings _settings = passwordSettings.Value;

    public async Task<int> Execute(SignUpRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _log.CreateUser(query.Email);

        User newUser = Map(query);
        int userId = await userRepository.Add(newUser, cancellation);
        // read back the user to get all fields populated (e.g., ETag)
        newUser = await userRepository.GetById(userId, cancellation)
                  ?? throw new InvalidOperationException($"Failed to retrieve newly created user with ID {userId}");

        Message message = ComposeMessage(newUser, query.Authority);
        _log.Enqueue(message.Id);
        await channelWriter.WriteAsync(message, cancellation);

        _log.CreatedUser(newUser.Email, newUser.Id);
        activityRecorder.Add<User>(new(newUser.Id, newUser.Id));
        return newUser.Id;
    }

    private Message ComposeMessage(User user, string authority) =>
        EmbeddedResource.Properties_SignUp_handlebars.ComposeMessage(
            user.Email,
            StringResources.SignUpSubject,
            new Dictionary<string, string>
            {
                ["name"] = user.Email,
                ["resetlink"] = authority
                                + _settings.ResetPath
                                + expiringQueryString.Create(
                                    TimeSpan.FromHours(_settings.SignUpTimeoutHours),
                                    user.GetPasswordResetParameters()),
                ["expiryhours"] = _settings.SignUpTimeoutHours.ToString(CultureInfo.InvariantCulture)
            });

    private static User Map(SignUpRequest query) =>
        new(query.Email.Trim(),
            null,
            UserRole.Customer,
            null,
            null);

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(140, LogLevel.Information, "Create new user {Email}")]
        public partial void CreateUser(string email);

        [LoggerMessage(141, LogLevel.Information, "{Email} is user {Id}")]
        public partial void CreatedUser(string email, int id);

        [LoggerMessage(142, LogLevel.Information, "Enqueue sign-up message {MessageId}")]
        public partial void Enqueue(Guid messageId);
    }
}
