namespace Klinkby.Booqr.Application.Models;

/// <summary>
/// Represents the configuration settings for reminder email scheduling.
/// </summary>
internal sealed record ReminderMailSettings
{
    /// <summary>
    /// Gets or sets the Danish (Europe/Copenhagen) time of day when reminder emails should be sent. Defaults to 18:00:00 (6:00 PM).
    /// </summary>
    public TimeSpan TimeOfDay { get; set; } = new(18, 00, 00);
}
