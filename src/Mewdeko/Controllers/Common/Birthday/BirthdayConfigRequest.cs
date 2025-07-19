namespace Mewdeko.Controllers.Common.Birthday;

/// <summary>
///     Request model for updating birthday configuration.
/// </summary>
public class BirthdayConfigRequest
{
    /// <summary>
    ///     The channel ID for birthday announcements.
    /// </summary>
    public ulong? BirthdayChannelId { get; set; }

    /// <summary>
    ///     The role ID to assign on birthdays.
    /// </summary>
    public ulong? BirthdayRoleId { get; set; }

    /// <summary>
    ///     The birthday message template.
    /// </summary>
    public string? BirthdayMessage { get; set; }

    /// <summary>
    ///     The role ID to ping for birthday announcements.
    /// </summary>
    public ulong? BirthdayPingRoleId { get; set; }

    /// <summary>
    ///     Number of days before birthday to send reminders.
    /// </summary>
    public int? BirthdayReminderDays { get; set; }

    /// <summary>
    ///     Default timezone for the guild.
    /// </summary>
    public string? DefaultTimezone { get; set; }
}