namespace Klinkby.Booqr.Application;

public sealed class ApplicationSettings
{
    [Required] public JwtSettings Jwt { get; set; } = new();
}
