namespace Klinkby.Booqr.Application.Models;

public sealed record ReminderMailSettings
{
    public required TimeSpan TimeOfDay { get; init; } = new(16, 00, 00);
}
