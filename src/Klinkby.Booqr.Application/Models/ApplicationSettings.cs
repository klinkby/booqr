namespace Klinkby.Booqr.Application.Models;

public sealed record ApplicationSettings
{
    public required JwtSettings Jwt { get; set; }

    public required ReminderMailSettings ReminderMail { get; set; }
}
