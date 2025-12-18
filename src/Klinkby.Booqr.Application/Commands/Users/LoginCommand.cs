using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record LoginRequest(
    [Required] [StringLength(0xff)] string Email,
    [Required] [StringLength(0xff)] string Password);

public sealed record LoginResponse(
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("token_type")]
    string TokenType,
    [property: JsonPropertyName("expires_in")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? ExpiresIn,
    [property: JsonIgnore]
    string? RefreshToken = null
);

public sealed partial class LoginCommand(
    IUserRepository userRepository,
    IOptions<JwtSettings> jwtSettings,
    TimeProvider timeProvider,
    ILogger<LoginCommand> logger) : ICommand<LoginRequest, Task<LoginResponse?>>
{
    private readonly LoggerMessages _log = new(logger);
    private readonly JwtSettings _jwt = jwtSettings.Value;

    public async Task<LoginResponse?> Execute(LoginRequest query, CancellationToken cancellation = default)
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

        var expires = _jwt.Expires;
        var accessToken = GenerateToken(user, expires);
        var refreshToken = GenerateRefreshToken(user);
        var response = new LoginResponse(accessToken, "Bearer", (int)expires.TotalSeconds, refreshToken);
        _log.LoggedIn(user.Id);
        return response;
    }

    private string GenerateToken(User user, TimeSpan expires)
    {
        var key = Encoding.ASCII.GetBytes(_jwt.Key!);
        JwtSecurityTokenHandler tokenHandler = new();
        IEnumerable<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Role, user.Role)
        ];
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = timeProvider.GetUtcNow().UtcDateTime + expires,
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            NotBefore = timeProvider.GetUtcNow().UtcDateTime,
        };

        SecurityToken? token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string GenerateRefreshToken(User user)
    {
        var key = Encoding.ASCII.GetBytes(_jwt.Key!);
        JwtSecurityTokenHandler tokenHandler = new();
        IEnumerable<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new("token_type", "refresh")
        ];
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = timeProvider.GetUtcNow().UtcDateTime + _jwt.RefreshExpires,
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            NotBefore = timeProvider.GetUtcNow().UtcDateTime,
        };

        SecurityToken? token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
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
