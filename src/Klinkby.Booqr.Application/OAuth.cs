using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Klinkby.Booqr.Application;

public interface IOAuth
{
    Task<OAuthTokenResponse> GenerateTokenResponse(User user, CancellationToken cancellation);
    Task<int?> GetUserIdFromValidRefreshToken(string refreshToken, CancellationToken cancellation);
    Task InvalidateToken(string refreshToken, CancellationToken cancellation);
}

internal sealed partial class OAuth(
    IRefreshTokenRepository refreshTokenRepository,
    TimeProvider timeProvider,
    IOptions<JwtSettings> jwtSettings,
    ILogger<OAuth> logger
    ) : IOAuth
{
    private const int Entropy = 30; // 240 bits, translates to 40 chars
    private static Encoding Encoding => Encoding.UTF8;
    private readonly JwtSettings _jwt = jwtSettings.Value;
    private readonly LoggerMessages _log = new(logger);

    public async Task<OAuthTokenResponse> GenerateTokenResponse(User user, CancellationToken cancellation)
    {
        _log.GenerateTokenResponse(user.Id);

        DateTime timestamp = timeProvider.GetUtcNow().UtcDateTime;
        var accessToken = GenerateAccessToken(user); // jwt
        var refreshToken = GenerateRefreshToken(); // opaque (random)
        var response = new OAuthTokenResponse(
            accessToken,
            (int)_jwt.AccessExpires.TotalSeconds,
            refreshToken,
            timestamp + _jwt.RefreshExpires);

        var tokenHash = Hash(refreshToken);
        RefreshToken refreshTokenMetadata = new(
            tokenHash,
            Guid.CreateVersion7(),
            user.Id,
            response.RefreshTokenExpiration,
            timestamp);
        await refreshTokenRepository.Add(refreshTokenMetadata, cancellation);

        _log.NewToken(tokenHash);

        return response;
    }

    public Task InvalidateToken(string refreshToken, CancellationToken cancellation)
    {
        var tokenHash = Hash(refreshToken);
        _log.Revoke(tokenHash);

        return refreshTokenRepository.RevokeSingle(tokenHash, timeProvider.GetUtcNow().UtcDateTime, cancellation);
    }

    public async Task<int?> GetUserIdFromValidRefreshToken(string refreshToken, CancellationToken cancellation)
    {
        _log.ValidateToken("Refresh");

        var tokenHash = Hash(refreshToken);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        RefreshToken? token = await refreshTokenRepository.GetByHash(tokenHash, cancellation);

        switch (token)
        {
            case null:
                _log.InvalidToken(tokenHash, "Not found");
                return null;

            case { Revoked: { } revoked }:
                _log.Revoked(tokenHash, revoked);
                await refreshTokenRepository.RevokeAll(token.Family, now, cancellation);
                return null;

            case { Expires: var expires } when expires <= now:
                _log.Expired(tokenHash, expires);
                return null;

            default:
                return token.UserId;
        }
    }

    // Opaque token
    private static string GenerateRefreshToken()
    {
        Span<byte> buffer = stackalloc byte[Entropy];
        RandomNumberGenerator.Fill(buffer);
        return Base64UrlEncode(buffer);
    }

    private string GenerateAccessToken(User user) =>
        GenerateToken(
            user,
            _jwt.AccessExpires,
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role));

    private string GenerateToken(User user, TimeSpan expires, params IEnumerable<Claim> additionalClaims)
    {
        var key = Encoding.GetBytes(_jwt.Key);
        JwtSecurityTokenHandler tokenHandler = new()
        {
            MapInboundClaims = false
        };
        IEnumerable<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
        ];
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Audience = _jwt.Audience,
            Expires = timeProvider.GetUtcNow().UtcDateTime + expires,
            Issuer = _jwt.Issuer,
            NotBefore = timeProvider.GetUtcNow().UtcDateTime,
            SigningCredentials = new(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Subject = new ClaimsIdentity(claims.Concat(additionalClaims)),
        };

        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string Hash(string token, int outputLength = Entropy)
    {
        Span<byte> tokenBytes = stackalloc byte[Encoding.GetByteCount(token)];
        Encoding.GetBytes(token, tokenBytes);
        Span<byte> digest = stackalloc byte[outputLength];
        Shake128.HashData(tokenBytes, digest);
        return Base64UrlEncode(digest);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> buffer) =>
        Base64Url.EncodeToString(buffer);

    private sealed partial class LoggerMessages(ILogger<OAuth> logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger<OAuth> _logger = logger;

        [LoggerMessage(280, LogLevel.Information, "Generate token response for {UserId}")]
        internal partial void GenerateTokenResponse(int userId);

        [LoggerMessage(281, LogLevel.Information, "Validate {TokenType} token")]
        internal partial void ValidateToken(string tokenType);

        [LoggerMessage(282, LogLevel.Warning, "Token {Hash} is not for user {UserId}")]
        internal partial void DifferentUser(string hash, int userId);

        [LoggerMessage(283, LogLevel.Warning, "Token {Hash} was revoked at {Timestamp} - possible fraud!!!")]
        internal partial void Revoked(string hash, DateTime timestamp);

        [LoggerMessage(284, LogLevel.Warning, "Token {Hash} has expired as {Timestamp} - possible fraud!!!")]
        internal partial void Expired(string hash, DateTime timestamp);

        [LoggerMessage(285, LogLevel.Information, "Revoke token {Hash}")]
        internal partial void Revoke(string hash);

        [LoggerMessage(286, LogLevel.Warning, "Invalid token {Hash} {Reason}")]
        internal partial void InvalidToken(string hash, string reason);

        [LoggerMessage(287, LogLevel.Information, "Generated {Hash}")]
        internal partial void NewToken(string hash);
    }
}
