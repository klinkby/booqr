using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Models;

public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn,
    [property: JsonIgnore]
    DateTime RefreshTokenExpiration,
    [property: JsonPropertyName("token_type")]
    string TokenType = "Bearer"
) : RefreshTokenDto;
