using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Models;

public abstract record RefreshTokenDto
{
    [property: JsonIgnore]
    public string? RefreshToken { get; set; }
}
