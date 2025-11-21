using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record ChangePasswordRequest(
    [Required]
    [StringLength(0x7f)]
    [RegularExpression(
        """
        ^(?=(.*[0-9]))(?=.*[\!@#$%^&*()\\[\]{}\-_+=~`|:;"'<>,./?])(?=.*[a-z])(?=(.*[A-Z])).{8,}$
        """, ErrorMessage = "Password is too simple")]
    string Password,
    [property: JsonIgnore]
    string QueryString);

public partial class ChangePasswordCommand(
    IUserRepository userRepository,
    IExpiringQueryString expiringQueryString,
    IActivityRecorder activityRecorder,
    ILogger<ChangePasswordCommand> logger
) : ICommand<ChangePasswordRequest, Task<bool>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<bool> Execute(ChangePasswordRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!expiringQueryString.TryParse(
                query.QueryString,
                out NameValueCollection? parameters,
                out QueryStringValidation validation)
            || !int.TryParse(parameters[Query.Id], CultureInfo.InvariantCulture, out var userId)
            || parameters[Query.Action] != Query.ChangePasswordAction)
        {
            _log.InvalidQueryString(validation);
            return false;
        }

        User? user = await userRepository.GetById(userId, cancellation);
        if (user is null)
        {
            _log.UserNotFound(userId);
            return false;
        }

        if (!user.ValidateETagParameter(parameters))
        {
            _log.Conflict(userId);
            return false;
        }

        _log.ChangePassword(userId);
        await userRepository.Update(user.WithPasswordHash(query.Password.Trim()), cancellation);

        _log.Changed(user.Email);
        activityRecorder.Update<User>(new(userId, user.Id));
        return true;
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(210, LogLevel.Information, "Change {UserId} password")]
        public partial void ChangePassword(int userId);

        [LoggerMessage(211, LogLevel.Warning, "Link invalid {Validation}")]
        public partial void InvalidQueryString(QueryStringValidation validation);

        [LoggerMessage(212, LogLevel.Information, "Password for {Email} successfully changed")]
        public partial void Changed(string email);

        [LoggerMessage(213, LogLevel.Information, "User {UserId} not found")]
        public partial void UserNotFound(int userId);

        [LoggerMessage(214, LogLevel.Information, "Conflict: User {UserId} has updated since link was generated")]
        public partial void Conflict(int userId);
    }
}
