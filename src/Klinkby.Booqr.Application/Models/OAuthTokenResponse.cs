using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Models;

public record RefreshRequest(
    [property: JsonIgnore] string? RefreshToken
);

public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn,
    string RefreshToken,
    [property: JsonIgnore]
    DateTime RefreshTokenExpiration,
    [property: JsonPropertyName("token_type")]
    string TokenType = "Bearer"
) : RefreshRequest(RefreshToken);
