namespace Klinkby.Booqr.Application.Models;

public sealed record ReminderMailSettings
{
    [Required] public TimeSpan TimeOfDay { get; set; } = new(16, 00, 00);
}
