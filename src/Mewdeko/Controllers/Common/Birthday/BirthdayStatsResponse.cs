namespace Mewdeko.Controllers.Common.Birthday;

/// <summary>
///     Response model for birthday statistics.
/// </summary>
public class BirthdayStatsResponse
{
    /// <summary>
    ///     Total number of users in the guild.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    ///     Number of users with birthdays set.
    /// </summary>
    public int UsersWithBirthdays { get; set; }

    /// <summary>
    ///     Number of users with birthday announcements enabled.
    /// </summary>
    public int UsersWithAnnouncementsEnabled { get; set; }

    /// <summary>
    ///     Number of birthdays today.
    /// </summary>
    public int TodaysBirthdayCount { get; set; }

    /// <summary>
    ///     Percentage of users who have set their birthday.
    /// </summary>
    public double BirthdaySetPercentage { get; set; }
}