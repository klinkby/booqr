namespace Klinkby.Booqr.Application;

public sealed record ApplicationSettings
{
    [Required] public JwtSettings Jwt { get; set; } = new();
}
