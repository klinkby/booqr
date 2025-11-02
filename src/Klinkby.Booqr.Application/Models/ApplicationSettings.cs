namespace Klinkby.Booqr.Application.Models;

public sealed record ApplicationSettings
{
    public required JwtSettings Jwt { get; init; }

    public required ReminderMailSettings ReminderMail { get; init; }
}
