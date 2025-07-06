using DataModel;
using LinqToDB;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for working with birthday-related entities.
/// </summary>
public static class BirthdayExtensions
{
    /// <summary>
    ///     Gets or creates a birthday configuration for a guild.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The birthday configuration for the guild.</returns>
    public static async Task<BirthdayConfig> GetOrCreateBirthdayConfig(this MewdekoDb db, ulong guildId)
    {
        var config = await db.BirthdayConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config == null)
        {
            config = new BirthdayConfig
            {
                GuildId = guildId,
                BirthdayReminderDays = 0,
                DefaultTimezone = "UTC",
                EnabledFeatures = 0,
                DateAdded = DateTime.UtcNow,
                DateModified = DateTime.UtcNow
            };

            config.Id = await db.InsertWithInt32IdentityAsync(config);
        }

        return config;
    }

    /// <summary>
    ///     Gets users with birthdays for a specific date and guild.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="date">The date to check for birthdays.</param>
    /// <param name="guildUserIds">The set of user IDs in the guild.</param>
    /// <returns>List of users with birthdays on the specified date.</returns>
    public static async Task<List<DiscordUser>> GetBirthdayUsersForDate(
        this MewdekoDb db,
        ulong guildId,
        DateTime date,
        HashSet<ulong> guildUserIds)
    {
        return await db.DiscordUsers
            .Where(u => guildUserIds.Contains(u.UserId) &&
                        u.Birthday.HasValue &&
                        u.Birthday.Value.Month == date.Month &&
                        u.Birthday.Value.Day == date.Day &&
                        u.BirthdayAnnouncementsEnabled &&
                        u.ProfilePrivacy != 1 && // Not private
                        u.BirthdayDisplayMode != 4) // Not disabled
            .ToListAsync();
    }

    /// <summary>
    ///     Gets upcoming birthdays for a guild within the specified number of days.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="days">The number of days to look ahead.</param>
    /// <param name="guildUserIds">The set of user IDs in the guild.</param>
    /// <returns>List of users with upcoming birthdays.</returns>
    public static async Task<List<DiscordUser>> GetUpcomingBirthdays(
        this MewdekoDb db,
        ulong guildId,
        int days,
        HashSet<ulong> guildUserIds)
    {
        var today = DateTime.UtcNow.Date;
        var upcomingDates = Enumerable.Range(0, days)
            .Select(i => today.AddDays(i))
            .ToList();

        var users = new List<DiscordUser>();

        foreach (var date in upcomingDates)
        {
            var dailyBirthdays = await GetBirthdayUsersForDate(db, guildId, date, guildUserIds);
            users.AddRange(dailyBirthdays);
        }

        return users.OrderBy(u => u.Birthday?.DayOfYear).ToList();
    }

    /// <summary>
    ///     Toggles birthday announcements for a user.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="user">The Discord user.</param>
    /// <returns>The new announcement status.</returns>
    public static async Task<bool> ToggleBirthdayAnnouncements(this MewdekoDb db, IUser user)
    {
        var discordUser = await db.GetOrCreateUser(user);
        discordUser.BirthdayAnnouncementsEnabled = !discordUser.BirthdayAnnouncementsEnabled;
        await db.UpdateAsync(discordUser);
        return discordUser.BirthdayAnnouncementsEnabled;
    }

    /// <summary>
    ///     Sets the timezone for a user's birthday calculations.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="user">The Discord user.</param>
    /// <param name="timezone">The timezone string.</param>
    /// <returns>True if successful.</returns>
    public static async Task<bool> SetUserBirthdayTimezone(this MewdekoDb db, IUser user, string timezone)
    {
        var discordUser = await db.GetOrCreateUser(user);
        discordUser.BirthdayTimezone = timezone;
        await db.UpdateAsync(discordUser);
        return true;
    }
}