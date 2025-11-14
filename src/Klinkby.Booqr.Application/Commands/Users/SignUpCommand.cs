using System.Threading.Channels;
using Klinkby.Booqr.Application.Util;

namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record SignUpRequest(
    [Required]
    [StringLength(0xff)]
    string Name,
    // https://emailregex.com/
    // // https://gist.github.com/StephenWDickey/8cd8f97a36f357b0df8d2559b0e1c2ab
    [Required]
    [StringLength(0xff)]
    [RegularExpression(
        """
        ^(?(")(".+?(?<!\\)"@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$
        """, ErrorMessage = "Email is not valid"
    )]
    string Email,
    [Required]
    [Range(10_00_00_00, 99_99_99_99)]
    long Phone
    );

public sealed partial class SignUpCommand(
    IUserRepository userRepository,
    ChannelWriter<Message> channelWriter,
    IActivityRecorder activityRecorder,
    ILogger<SignUpCommand> logger
) : ICommand<SignUpRequest, Task<int>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<int> Execute(SignUpRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _log.CreateUser(query.Email);
        var password = ResetPasswordCommand.GenerateRandomPassword();
        User newUser = Map(query, password);
        var userId = await userRepository.Add(newUser, cancellation);

        Message message = ComposeMessage(newUser, password);
        _log.Enqueue(message.Id);
        await channelWriter.WriteAsync(message, cancellation);

        _log.CreatedUser(newUser.Email, userId);
        activityRecorder.Add<User>(new(userId, userId));
        return userId;
    }

    private static Message ComposeMessage(User user, string password) =>
        EmbeddedResource.Properties_SignUp_handlebars.ComposeMessage(
            user.Email,
            StringResources.SignUpSubject,
            new Dictionary<string, string>
            {
                ["name"] = user.Name ?? user.Email,
                ["password"] = password
            });

    private static User Map(SignUpRequest query, string password)
    {
        var user = new User(
            query.Email.Trim(),
            string.Empty,
            UserRole.Customer,
            query.Name.Trim(),
            query.Phone);
        return ResetPasswordCommand.WithPasswordHash(user, password);
    }

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
