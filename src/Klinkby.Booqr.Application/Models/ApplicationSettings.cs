namespace Klinkby.Booqr.Application.Models;

public sealed class ApplicationSettings
{
    [Required] public JwtSettings Jwt { get; set; } = new();
}
