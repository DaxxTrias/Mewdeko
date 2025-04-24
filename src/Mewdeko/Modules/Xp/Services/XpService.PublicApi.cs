using System.IO;
using System.Text.Json;
using DataModel;
using LinqToDB;
using Mewdeko.Database.EF.EFCore.Enums;
using Mewdeko.Modules.Xp.Models;
using Serilog;
using UserXpStats = Mewdeko.Modules.Xp.Models.UserXpStats;
using XpNotificationType = Mewdeko.Modules.Xp.Models.XpNotificationType;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Contains public API methods for the XP service.
/// </summary>
public partial class XpService
{
    #region Static Access

    /// <summary>
    ///     Gets the singleton instance of the XP service.
    /// </summary>
    public static XpService Instance { get; private set; }


    /// <summary>
    ///     Gets the Discord client.
    /// </summary>
    /// <returns>The Discord client.</returns>
    public DiscordShardedClient GetClient()
    {
        return Client;
    }

    /// <summary>
    ///     Gets the background processor.
    /// </summary>
    /// <returns>The background processor.</returns>
    public XpBackgroundProcessor GetBackgroundProcessor()
    {
        return backgroundProcessor;
    }

    /// <summary>
    ///     Queues an XP gain for a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="amount">The amount of XP to add.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="source">The source of the XP gain.</param>
    public void QueueXpGain(ulong guildId, ulong userId, int amount, ulong channelId, XpSource source)
    {
        backgroundProcessor.QueueXpGain(guildId, userId, amount, channelId, source);
    }

    #endregion

    #region User XP Management

    /// <summary>
    ///     Adds XP to a user in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="amount">The amount of XP to add.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddXpAsync(ulong guildId, ulong userId, int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        backgroundProcessor.QueueXpGain(guildId, userId, amount, 0, XpSource.Manual);

        await Task.CompletedTask;
    }

    /// <summary>
    ///     Gets a user's XP statistics in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user's XP statistics.</returns>
    public async Task<UserXpStats> GetUserXpStatsAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userXp = await cacheManager.GetOrCreateGuildUserXpAsync(db, guildId, userId);
        var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);

        var level = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);
        var levelXp = userXp.TotalXp - XpCalculator.CalculateXpForLevel(level, (XpCurveType)settings.XpCurveType);
        var requiredXp = XpCalculator.CalculateXpForLevel(level + 1, (XpCurveType)settings.XpCurveType) -
                         XpCalculator.CalculateXpForLevel(level, (XpCurveType)settings.XpCurveType);

        return new UserXpStats
        {
            UserId = userId,
            GuildId = guildId,
            TotalXp = userXp.TotalXp,
            Level = level,
            LevelXp = levelXp,
            RequiredXp = requiredXp,
            Rank = await GetUserRankAsync(guildId, userId),
            BonusXp = userXp.BonusXp
        };
    }

    /// <summary>
    ///     Gets a user's XP rank in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user's rank.</returns>
    public async Task<int> GetUserRankAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userXp = await db.GuildUserXps
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (userXp == null)
            return 0;

        return await db.GuildUserXps
            .CountAsync(x => x.GuildId == guildId && x.TotalXp > userXp.TotalXp) + 1;
    }

    /// <summary>
    ///     Gets the XP leaderboard for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of users per page.</param>
    /// <returns>The XP leaderboard.</returns>
    public async Task<List<UserXpStats>> GetLeaderboardAsync(ulong guildId, int page = 1, int pageSize = 10)
    {
        if (page < 1)
            page = 1;

        if (pageSize < 1 || pageSize > 100)
            pageSize = 10;

        // Check cache for leaderboard
        var cacheKey = $"xp:leaderboard:{guildId}:{page}:{pageSize}";

        var red = cacheManager.GetRedisDatabase();
        var possibleData = await red.StringGetAsync(cacheKey);
        if (possibleData.HasValue)
        {
            var possibleDeserialize = JsonSerializer.Deserialize<List<UserXpStats>>(possibleData);
            if (possibleDeserialize != null)
                return possibleDeserialize;
        }

        // Calculate skip
        var skip = (page - 1) * pageSize;

        await using var db = await dbFactory.CreateConnectionAsync();

        var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);

        var users = await db.GuildUserXps
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.TotalXp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<UserXpStats>();

        for (var i = 0; i < users.Count; i++)
        {
            var user = users[i];
            var level = XpCalculator.CalculateLevel(user.TotalXp, (XpCurveType)settings.XpCurveType);
            var levelXp = user.TotalXp - XpCalculator.CalculateXpForLevel(level, (XpCurveType)settings.XpCurveType);
            var requiredXp = XpCalculator.CalculateXpForLevel(level + 1, (XpCurveType)settings.XpCurveType) -
                             XpCalculator.CalculateXpForLevel(level, (XpCurveType)settings.XpCurveType);

            result.Add(new UserXpStats
            {
                UserId = user.UserId,
                GuildId = guildId,
                TotalXp = user.TotalXp,
                Level = level,
                LevelXp = levelXp,
                RequiredXp = requiredXp,
                Rank = skip + i + 1,
                BonusXp = user.BonusXp
            });
        }

        var cereal = JsonSerializer.Serialize(result);
        // Cache the result
        await red.StringSetAsync(cacheKey, cereal, TimeSpan.FromMinutes(5));

        return result;
    }

    /// <summary>
    ///     Resets a user's XP in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="resetBonusXp">Whether to also reset bonus XP.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ResetUserXpAsync(ulong guildId, ulong userId, bool resetBonusXp = false)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userXp = await cacheManager.GetOrCreateGuildUserXpAsync(db, guildId, userId);

        userXp.TotalXp = 0;

        if (resetBonusXp)
        {
            userXp.BonusXp = 0;
        }

        userXp.LastLevelUp = DateTime.UtcNow;
        await db.UpdateAsync(userXp);

        // Update cache
        cacheManager.UpdateUserXpCacheAsync(userXp);
    }

    /// <summary>
    ///     Sets a user's XP to a specific amount.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="xpAmount">The XP amount to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetUserXpAsync(ulong guildId, ulong userId, long xpAmount)
    {
        if (xpAmount < 0)
            throw new ArgumentException("XP amount cannot be negative", nameof(xpAmount));

        await using var db = await dbFactory.CreateConnectionAsync();

        var userXp = await cacheManager.GetOrCreateGuildUserXpAsync(db, guildId, userId);

        userXp.TotalXp = xpAmount;
        userXp.LastLevelUp = DateTime.UtcNow;
        await db.UpdateAsync(userXp);

        // Update cache
        cacheManager.UpdateUserXpCacheAsync(userXp);
    }

    /// <summary>
    ///     Sets a user's XP notification preference.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="type">The notification type preference.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetUserNotificationPreferenceAsync(ulong guildId, ulong userId, XpNotificationType type)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userXp = await cacheManager.GetOrCreateGuildUserXpAsync(db, guildId, userId);

        userXp.NotifyType = (int)type;
        await db.UpdateAsync(userXp);

        // Update cache
        cacheManager.UpdateUserXpCacheAsync(userXp);
    }

    /// <summary>
    ///     Gets the time a user has spent on their current level.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>A tuple with total days, hours, and minutes on the current level.</returns>
    public async Task<(int Days, int Hours, int Minutes)> GetTimeOnCurrentLevelAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userXp = await cacheManager.GetOrCreateGuildUserXpAsync(db, guildId, userId);

        var timeSpan = DateTime.UtcNow - userXp.LastLevelUp;

        return (timeSpan.Days, timeSpan.Hours, timeSpan.Minutes);
    }

    #endregion

    #region Guild Settings Management

    /// <summary>
    ///     Updates guild XP settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="settings">Action to modify the settings.</param>
    /// <returns>The updated settings.</returns>
    public async Task<GuildXpSetting> UpdateGuildXpSettingsAsync(ulong guildId, Action<GuildXpSetting> settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var currentSettings = await cacheManager.GetGuildXpSettingsAsync(guildId);

        // Apply changes
        settings(currentSettings);

        // Validate settings
        if (currentSettings.XpPerMessage > MaxXpPerMessage)
            currentSettings.XpPerMessage = MaxXpPerMessage;

        if (currentSettings.VoiceXpPerMinute > MaxVoiceXpPerMinute)
            currentSettings.VoiceXpPerMinute = MaxVoiceXpPerMinute;

        if (currentSettings.MessageXpCooldown < 0)
            currentSettings.MessageXpCooldown = DefaultMessageXpCooldown;

        if (currentSettings.XpMultiplier <= 0)
            currentSettings.XpMultiplier = 1.0;

        // Update settings
        await cacheManager.UpdateGuildXpSettingsAsync(currentSettings);

        return currentSettings;
    }

    /// <summary>
    ///     Resets XP for all users in the guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="resetBonusXp">Whether to also reset bonus XP for all users.</param>
    /// <returns>A task representing the asynchronous operation and the number of affected users.</returns>
    public async Task<int> ResetGuildXp(ulong guildId, bool resetBonusXp = false)
    {
        Log.Information("Resetting XP for all users in guild {GuildId}", guildId);

        await using var db = await dbFactory.CreateConnectionAsync();

        var now = DateTime.UtcNow;

        // Perform bulk update using LinqToDB
        int affectedUsers;

        if (resetBonusXp)
        {
            // Reset both total XP and bonus XP
            affectedUsers = await db.GuildUserXps
                .Where(x => x.GuildId == guildId)
                .Set(x => x.TotalXp, 0L)
                .Set(x => x.BonusXp, 0)
                .Set(x => x.LastLevelUp, now)
                .UpdateAsync();
        }
        else
        {
            // Reset only total XP
            affectedUsers = await db.GuildUserXps
                .Where(x => x.GuildId == guildId)
                .Set(x => x.TotalXp, 0L)
                .Set(x => x.LastLevelUp, now)
                .UpdateAsync();
        }

        // Clear all caches for this guild using the cache manager
        await cacheManager.ClearGuildXpCachesAsync(guildId);

        Log.Information("Successfully reset XP for {UserCount} users in guild {GuildId}", affectedUsers, guildId);

        return affectedUsers;
    }

    /// <summary>
    ///     Gets the guild XP settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild XP settings.</returns>
    public async Task<GuildXpSetting> GetGuildXpSettingsAsync(ulong guildId)
    {
        // First try to get from cache manager
        return await cacheManager.GetGuildXpSettingsAsync(guildId);
    }

    #endregion

    #region Rewards Management

    /// <summary>
    ///     Sets a role reward for a specific level.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level to set the reward for.</param>
    /// <param name="roleId">The role ID to award, or null to remove the reward.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetRoleRewardAsync(ulong guildId, int level, ulong? roleId)
    {
        await rewardManager.SetRoleRewardAsync(guildId, level, roleId);
    }

    /// <summary>
    ///     Sets a currency reward for a specific level.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level to set the reward for.</param>
    /// <param name="amount">The amount of currency to award, or 0 to remove the reward.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCurrencyRewardAsync(ulong guildId, int level, long amount)
    {
        await rewardManager.SetCurrencyRewardAsync(guildId, level, amount);
    }

    /// <summary>
    ///     Gets all role rewards for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of role rewards.</returns>
    public async Task<List<XpRoleReward>> GetRoleRewardsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var rewards = await db.XpRoleRewards
            .Where(r => r.GuildId == guildId)
            .ToListAsync();

        return rewards;
    }

    /// <summary>
    ///     Gets all currency rewards for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of currency rewards.</returns>
    public async Task<List<XpCurrencyReward>> GetCurrencyRewardsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var rewards = await db.XpCurrencyRewards
            .Where(r => r.GuildId == guildId)
            .ToListAsync();

        return rewards;
    }

    #endregion

    #region Multiplier Management

    /// <summary>
    ///     Sets a channel multiplier.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="multiplier">The multiplier value.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetChannelMultiplierAsync(ulong guildId, ulong channelId, double multiplier)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existingMultiplier = await db.XpChannelMultipliers
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId);

        if (multiplier != 1.0)
        {
            if (existingMultiplier != null)
            {
                existingMultiplier.Multiplier = multiplier;
                await db.UpdateAsync(existingMultiplier);
            }
            else
            {
                var newMultiplier = new XpChannelMultiplier
                {
                    GuildId = guildId, ChannelId = channelId, Multiplier = multiplier
                };
                await db.InsertAsync(newMultiplier);
            }
        }
        else if (existingMultiplier != null)
        {
            await db.DeleteAsync(existingMultiplier);
        }
    }

    /// <summary>
    ///     Sets a role multiplier.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <param name="multiplier">The multiplier value.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetRoleMultiplierAsync(ulong guildId, ulong roleId, double multiplier)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existingMultiplier = await db.XpRoleMultipliers
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);

        if (multiplier != 1.0)
        {
            if (existingMultiplier != null)
            {
                existingMultiplier.Multiplier = multiplier;
                await db.UpdateAsync(existingMultiplier);
            }
            else
            {
                var newMultiplier = new XpRoleMultiplier
                {
                    GuildId = guildId, RoleId = roleId, Multiplier = multiplier
                };
                await db.InsertAsync(newMultiplier);
            }
        }
        else if (existingMultiplier != null)
        {
            await db.DeleteAsync(existingMultiplier);
        }
    }

    /// <summary>
    ///     Creates a boost event for XP gain.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The event name.</param>
    /// <param name="multiplier">The XP multiplier.</param>
    /// <param name="startTime">When the event starts.</param>
    /// <param name="endTime">When the event ends.</param>
    /// <param name="applicableChannels">Channel IDs this boost applies to (empty for all).</param>
    /// <param name="applicableRoles">Role IDs this boost applies to (empty for all).</param>
    /// <returns>The created boost event.</returns>
    public async Task<XpBoostEvent> CreateXpBoostEventAsync(
        ulong guildId,
        string name,
        double multiplier,
        DateTime startTime,
        DateTime endTime,
        List<ulong> applicableChannels = null,
        List<ulong> applicableRoles = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Event name cannot be empty", nameof(name));

        if (multiplier <= 0)
            throw new ArgumentException("Multiplier must be positive", nameof(multiplier));

        if (startTime >= endTime)
            throw new ArgumentException("End time must be after start time", nameof(endTime));

        await using var db = await dbFactory.CreateConnectionAsync();

        var channelsString = applicableChannels != null && applicableChannels.Any()
            ? string.Join(",", applicableChannels)
            : "";

        var rolesString = applicableRoles != null && applicableRoles.Any()
            ? string.Join(",", applicableRoles)
            : "";

        var boostEvent = new XpBoostEvent
        {
            GuildId = guildId,
            Name = name,
            Multiplier = multiplier,
            StartTime = startTime,
            EndTime = endTime,
            ApplicableChannels = channelsString,
            ApplicableRoles = rolesString
        };

        await db.InsertAsync(boostEvent);
        return boostEvent;
    }

    /// <summary>
    ///     Cancels an XP boost event by ID.
    /// </summary>
    /// <param name="eventId">The event ID to cancel.</param>
    /// <returns>True if the event was found and canceled, false otherwise.</returns>
    public async Task<bool> CancelXpBoostEventAsync(int eventId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var boostEvent = await db.XpBoostEvents
            .FirstOrDefaultAsync(x => x.Id == eventId);

        if (boostEvent == null)
            return false;

        await db.DeleteAsync(boostEvent);
        return true;
    }

    /// <summary>
    ///     Gets active XP boost events for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of active boost events.</returns>
    public async Task<List<XpBoostEvent>> GetActiveBoostEventsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var now = DateTime.UtcNow;

        return await db.XpBoostEvents
            .Where(x => x.GuildId == guildId && x.StartTime <= now && x.EndTime >= now)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets all XP boost events for a guild (including past and future events).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of all boost events.</returns>
    public async Task<List<XpBoostEvent>> GetAllBoostEventsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.XpBoostEvents
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.StartTime)
            .ToListAsync();
    }

    #endregion

    #region Exclusion Management

    /// <summary>
    ///     Excludes an item (user, role, or channel) from XP gain.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="itemId">The item ID to exclude.</param>
    /// <param name="itemType">The type of item to exclude.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExcludeItemAsync(ulong guildId, ulong itemId, ExcludedItemType itemType)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId && x.ItemId == itemId && x.ItemType == (int)itemType);

        if (!exists)
        {
            var newItem = new XpExcludedItem
            {
                GuildId = guildId, ItemId = itemId, ItemType = (int)itemType
            };
            await db.InsertAsync(newItem);
        }
    }

    /// <summary>
    ///     Includes a previously excluded item for XP gain.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="itemId">The item ID to include.</param>
    /// <param name="itemType">The type of item to include.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task IncludeItemAsync(ulong guildId, ulong itemId, ExcludedItemType itemType)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var excludedItem = await db.XpExcludedItems
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.ItemId == itemId && x.ItemType == (int)itemType);

        if (excludedItem != null)
        {
            await db.DeleteAsync(excludedItem);
        }
    }

    /// <summary>
    ///     Gets the excluded items of a specific type for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="itemType">The type of excluded items to get.</param>
    /// <returns>A list of excluded item IDs.</returns>
    public async Task<List<ulong>> GetExcludedItemsAsync(ulong guildId, ExcludedItemType itemType)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var excludedItems = await db.XpExcludedItems
            .Where(x => x.GuildId == guildId && x.ItemType == (int)itemType)
            .Select(x => x.ItemId)
            .ToListAsync();

        return excludedItems;
    }

    #endregion

    #region Competition Management

    /// <summary>
    ///     Creates a new XP competition.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The competition name.</param>
    /// <param name="type">The competition type.</param>
    /// <param name="startTime">The start time.</param>
    /// <param name="endTime">The end time.</param>
    /// <param name="targetLevel">The target level for ReachLevel competitions.</param>
    /// <param name="announcementChannelId">The channel ID for announcements.</param>
    /// <returns>The created competition.</returns>
    public async Task<XpCompetition> CreateCompetitionAsync(
        ulong guildId,
        string name,
        XpCompetitionType type,
        DateTime startTime,
        DateTime endTime,
        int targetLevel = 0,
        ulong? announcementChannelId = null)
    {
        return await competitionManager.CreateCompetitionAsync(
            guildId, name, type, startTime, endTime, targetLevel, announcementChannelId);
    }

    /// <summary>
    ///     Gets all active competitions for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of active competitions.</returns>
    public async Task<List<XpCompetition>> GetActiveCompetitionsAsync(ulong guildId)
    {
        // We can delegate to the competition manager since it already has this functionality
        return await competitionManager.GetActiveCompetitionsAsync(guildId);
    }

    /// <summary>
    ///     Gets all entries for a competition.
    /// </summary>
    /// <param name="competitionId">The competition ID.</param>
    /// <returns>A list of competition entries.</returns>
    public async Task<List<XpCompetitionEntry>> GetCompetitionEntriesAsync(int competitionId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var entries = await db.XpCompetitionEntries
            .Where(e => e.CompetitionId == competitionId)
            .ToListAsync();

        return entries;
    }

    /// <summary>
    ///     Gets a specific competition by ID.
    /// </summary>
    /// <param name="competitionId">The competition ID.</param>
    /// <returns>The competition, or null if not found.</returns>
    public async Task<XpCompetition> GetCompetitionAsync(int competitionId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var competition = await db.XpCompetitions
            .FirstOrDefaultAsync(c => c.Id == competitionId);

        return competition;
    }

    /// <summary>
    ///     Adds a reward to a competition.
    /// </summary>
    /// <param name="competitionId">The competition ID.</param>
    /// <param name="position">The position to reward.</param>
    /// <param name="roleId">The role ID to award.</param>
    /// <param name="xpAmount">The XP amount to award.</param>
    /// <param name="currencyAmount">The currency amount to award.</param>
    /// <param name="customReward">A custom reward description.</param>
    /// <returns>The created reward.</returns>
    public async Task<XpCompetitionReward> AddCompetitionRewardAsync(
        int competitionId,
        int position,
        ulong roleId = 0,
        int xpAmount = 0,
        long currencyAmount = 0,
        string customReward = "")
    {
        return await competitionManager.AddCompetitionRewardAsync(
            competitionId, position, roleId, xpAmount, currencyAmount, customReward);
    }

    /// <summary>
    ///     Starts a competition manually.
    /// </summary>
    /// <param name="competitionId">The competition ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartCompetitionAsync(int competitionId)
    {
        await competitionManager.StartCompetitionAsync(competitionId);
    }

    /// <summary>
    ///     Finalizes a competition manually.
    /// </summary>
    /// <param name="competitionId">The competition ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task FinalizeCompetitionAsync(int competitionId)
    {
        await competitionManager.FinalizeCompetitionAsync(competitionId);
    }

    #endregion

    #region XP Card Generation

    /// <summary>
    ///     Generates an XP card for a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="templateId">The template ID to use (optional).</param>
    /// <returns>The path to the generated image file.</returns>
    public async Task<Stream> GenerateXpCardAsync(ulong guildId, ulong userId, int? templateId = null)
    {
        try
        {
            var guild = Client.GetGuild(guildId);
            var user = guild?.GetUser(userId);

            if (guild == null || user == null)
                throw new ArgumentException($"User {userId} not found in guild {guildId}");

            // Generate XP card using the specialized service
            var cardGenerator = new XpCardGenerator(dbFactory, this);
            var cardStream = await cardGenerator.GenerateXpImageAsync(user);

            // Reset the stream position so it can be read
            cardStream.Position = 0;

            return cardStream;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating XP card for {UserId} in {GuildId}", userId, guildId);
            throw;
        }
    }

    /// <summary>
    ///     Gets the default background image for XP cards.
    /// </summary>
    /// <returns>The default background image bytes.</returns>
    public byte[] GetDefaultBackgroundImage()
    {
        // This would be loaded from a resource file or similar
        // For now, returning a placeholder
        return File.ReadAllBytes("data/images/default_xp_background.png");
    }

    #endregion
}