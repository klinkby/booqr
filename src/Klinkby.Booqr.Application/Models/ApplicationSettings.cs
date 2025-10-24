namespace Klinkby.Booqr.Application.Models;

public sealed record ApplicationSettings
{
    [Required] public JwtSettings Jwt { get; set; } = new();
}
