namespace Klinkby.Booqr.Application.Models;

/// <summary>
/// Represents the configuration settings for reminder email scheduling.
/// </summary>
internal sealed record ReminderMailSettings
{
    /// <summary>
    /// Gets or sets the time of day when reminder emails should be sent. Defaults to 16:00:00 (4:00 PM).
    /// </summary>
    public TimeSpan TimeOfDay { get; set; } = new(16, 00, 00);
}
