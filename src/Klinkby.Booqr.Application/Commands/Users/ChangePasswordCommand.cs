using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
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
) : ICommand<ChangePasswordRequest, Task<Result<bool>>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<bool>> Execute(ChangePasswordRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!expiringQueryString.TryParse(
                query.QueryString,
                out NameValueCollection? parameters,
                out QueryStringValidation validation))
        {
            _log.InvalidQueryString(validation);
            return Problem.ValidationFailed with { Detail = $"Invalid or expired link: {validation}" };
        }

        if (!int.TryParse(parameters[Query.Id], CultureInfo.InvariantCulture, out var userId))
        {
            _log.UserIdNotAnInteger(parameters[Query.Id]);
            return Problem.ValidationFailed with { Detail = "User id is not an integer" };
        }

        if (parameters[Query.Action] != Query.ChangePasswordAction)
        {
            _log.InvalidAction(parameters[Query.Action]);
            return Problem.ValidationFailed with { Detail = "Invalid action" };
        }

        User? user = await userRepository.GetById(userId, cancellation);
        if (user is null)
        {
            _log.UserNotFound(userId);
            return Problem.NotFound with { Detail = $"User {userId} was not found" };
        }

        if (!user.ValidateETagParameter(parameters))
        {
            _log.Conflict(userId);
            return Problem.MidAirCollision with { Detail = $"User {userId} was updated since the link was generated" };
        }

        _log.ChangePassword(userId);

        bool patched = await userRepository.Patch(new PartialUser(userId).WithPasswordHash(query.Password.Trim()), cancellation);

        _log.Changed(user.Email);
        activityRecorder.Update<User>(new(userId, user.Id));
        return patched;
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(210, LogLevel.Information, "Change {UserId} password")]
        public partial void ChangePassword(int userId);

        [LoggerMessage(211, LogLevel.Warning, "Link invalid {Validation}")]
        public partial void InvalidQueryString(QueryStringValidation validation);

        [LoggerMessage(212, LogLevel.Information, "Password for {Email} successfully changed")]
        public partial void Changed(string email);

        [LoggerMessage(213, LogLevel.Warning, "User {UserId} not found")]
        public partial void UserNotFound(int userId);

        [LoggerMessage(214, LogLevel.Warning, "Conflict: User {UserId} has updated since link was generated")]
        public partial void Conflict(int userId);

        [LoggerMessage(215, LogLevel.Warning, "UserId not an integer: {UserId}")]
        public partial void UserIdNotAnInteger(string? userId);

        [LoggerMessage(216, LogLevel.Warning, "Invalid action: {Action}")]
        public partial void InvalidAction(string? action);
    }
}
