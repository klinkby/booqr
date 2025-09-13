using BCryptNet = BCrypt.Net.BCrypt;

namespace Klinkby.Booqr.Application.Users;

public sealed record SignUpRequest(
    [Required] [StringLength(0xff)] string Name,
    [Required]
    [Range(10_00_00_00, 99_99_99_99)]
    long Phone,
    // https://emailregex.com/
    [Required]
    [StringLength(0xff)]
    [RegularExpression(
        """
        ^(?(")(".+?(?<!\\)"@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$
        """, ErrorMessage = "Email is not valid"
    )]
    string Email,
    // https://gist.github.com/StephenWDickey/8cd8f97a36f357b0df8d2559b0e1c2ab
    [Required]
    [StringLength(0x7f)]
    [RegularExpression(
        """
        ^(?=(.*[0-9]))(?=.*[\!@#$%^&*()\\[\]{}\-_+=~`|:;"'<>,./?])(?=.*[a-z])(?=(.*[A-Z])).{8,}$
        """, ErrorMessage = "Password is too simple")]
    string Password);

public sealed partial class SignUpCommand(
    IUserRepository userRepository,
    ILogger<SignUpCommand> logger
) : ICommand<SignUpRequest, Task<int>>
{
    public Task<int> Execute(SignUpRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        LogCreateUser(logger, query.Email);
        return userRepository.Add(Map(query), cancellation);
    }

    private static User Map(SignUpRequest query)
    {
        return new User(
            query.Email.Trim(),
            BCryptNet.EnhancedHashPassword(query.Password.Trim()),
            UserRole.Customer,
            query.Name.Trim(),
            query.Phone);
    }

    [LoggerMessage(LogLevel.Information, "Create new user {Email}")]
    private static partial void LogCreateUser(ILogger logger, string email);
}
