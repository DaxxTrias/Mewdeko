using System.Text.Json;
using LinqToDB;
using DataModel;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.EF.EFCore.Enums;
using Mewdeko.Modules.Xp.Models;

using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Manages caching for XP-related data.
/// </summary>
public class XpCacheManager : INService
{
    private readonly IDataCache dataCache;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IDatabase redisCache;
    private readonly DiscordShardedClient client;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpCacheManager" /> class.
    /// </summary>
    /// <param name="dataCache">The data cache.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="client">The current sharded client</param>
    public XpCacheManager(
        IDataCache dataCache,
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client)
    {
        this.dataCache = dataCache;
        this.dbFactory = dbFactory;
        this.client = client;
        redisCache = dataCache.Redis.GetDatabase();
    }

    /// <summary>
    ///     Gets the Redis database instance.
    /// </summary>
    /// <returns>The Redis database.</returns>
    public IDatabase GetRedisDatabase()
    {
        return redisCache;
    }

    /// <summary>
    ///     Gets or creates a guild user XP record.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The guild user XP record.</returns>
    public async Task<GuildUserXp> GetOrCreateGuildUserXpAsync(MewdekoDb db, ulong guildId, ulong userId)
    {
        // Check memory cache first
        var cacheKey = $"xp:guild_user:{guildId}:{userId}";

        var redisData = await redisCache.StringGetAsync(cacheKey);
        if (redisData.HasValue)
        {
            var userData = JsonSerializer.Deserialize<GuildUserXp>(redisData);
            if (userData is not null)
                return userData;
        }

        // Get from database using LinqToDB
        var userXp = await db.GuildUserXps
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (userXp == null)
        {
            userXp = new GuildUserXp
            {
                GuildId = guildId,
                UserId = userId,
                LastActivity = DateTime.UtcNow,
                LastLevelUp = DateTime.UtcNow,
                NotifyType = (int)XpNotificationType.Channel
            };

            // Insert using LinqToDB
            await db.InsertAsync(userXp);
        }

        // Update caches
        UpdateUserXpCacheAsync(userXp);

        return userXp;
    }

    /// <summary>
    ///     Updates the user XP cache.
    /// </summary>
    /// <param name="userXp">The user XP record to cache.</param>
    public void UpdateUserXpCacheAsync(GuildUserXp userXp)
    {
        var cacheKey = $"xp:guild_user:{userXp.GuildId}:{userXp.UserId}";

        // Update Redis cache
        var serializedData = JsonSerializer.Serialize(userXp);
        redisCache.StringSetAsync(cacheKey, serializedData, TimeSpan.FromMinutes(30));
    }

    /// <summary>
    ///     Gets the guild XP settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild XP settings.</returns>
    public async Task<GuildXpSetting> GetGuildXpSettingsAsync(ulong guildId)
    {
        // Check memory cache first
        var cacheKey = $"xp:guild_settings:{guildId}";

        // Try Redis cache
        var redisData = await redisCache.StringGetAsync(cacheKey);
        if (redisData.HasValue)
        {
            var deserialize = JsonSerializer.Deserialize<GuildXpSetting>(redisData);
            if (deserialize != null)
            {
                return deserialize;
            }
        }

        // Get from database using LinqToDB
        await using var db = await dbFactory.CreateConnectionAsync();

        var settings = await db.GuildXpSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = new GuildXpSetting
            {
                GuildId = guildId,
                XpPerMessage = XpService.DefaultXpPerMessage,
                MessageXpCooldown = XpService.DefaultMessageXpCooldown,
                VoiceXpPerMinute = XpService.DefaultVoiceXpPerMinute,
                VoiceXpTimeout = XpService.DefaultVoiceXpTimeout,
                XpMultiplier = 1.0,
                FirstMessageBonus = 0,
                CustomXpImageUrl = "",
                LevelUpMessage = "{UserMention} has reached level {Level}!"
            };

            // Insert using LinqToDB
            await db.InsertAsync(settings);
        }

        var serializedData = JsonSerializer.Serialize(settings);
        await redisCache.StringSetAsync(cacheKey, serializedData, TimeSpan.FromHours(1));

        return settings;
    }

    /// <summary>
    ///     Updates the guild XP settings.
    /// </summary>
    /// <param name="settings">The guild XP settings to update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateGuildXpSettingsAsync(GuildXpSetting settings)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Update using LinqToDB
        await db.UpdateAsync(settings);

        // Update cache
        var cacheKey = $"xp:guild_settings:{settings.GuildId}";

        var serializedData = JsonSerializer.Serialize(settings);
        await redisCache.StringSetAsync(cacheKey, serializedData, TimeSpan.FromHours(1));
    }

    /// <summary>
    ///     Checks if a server is excluded from XP gain.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True if the server is excluded, false otherwise.</returns>
    public async Task<bool> IsServerExcludedAsync(ulong guildId)
    {
        var settings = await GetGuildXpSettingsAsync(guildId);
        return settings.XpGainDisabled;
    }

    /// <summary>
    ///     Gets the effective XP multiplier for a user in a channel.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The effective XP multiplier to apply.</returns>
    public async Task<double> GetEffectiveMultiplierAsync(ulong userId, ulong guildId, ulong channelId)
    {
        // Try to get from cache first
        var cacheKey = $"xp:multiplier:{guildId}:{userId}:{channelId}";

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var settings = await GetGuildXpSettingsAsync(guildId);
            var multiplier = settings.XpMultiplier;

            // Apply channel multiplier if exists using LinqToDB
            var channelMultiplier = await db.XpChannelMultipliers
                .FirstOrDefaultAsync(c => c.GuildId == guildId && c.ChannelId == channelId);

            if (channelMultiplier != null)
            {
                multiplier *= channelMultiplier.Multiplier;
            }

            var guild = client.GetGuild(guildId);
            var user = guild?.GetUser(userId);

            if (user != null)
            {
                // Apply role multiplier if exists (highest one) using LinqToDB
                var userRoleIds = user.Roles.Select(x => x.Id).ToList();

                var roleMultipliers = await db.XpRoleMultipliers
                    .Where(r => r.GuildId == guildId && userRoleIds.Contains(r.RoleId))
                    .ToListAsync();

                if (roleMultipliers.Count != 0)
                {
                    multiplier *= roleMultipliers.Max(r => r.Multiplier);
                }
            }

            // Apply active boost events using LinqToDB
            var now = DateTime.UtcNow;
            var boostEvents = await db.XpBoostEvents
                .Where(b => b.GuildId == guildId && b.StartTime <= now && b.EndTime >= now)
                .ToListAsync();

            foreach (var boost in boostEvents)
            {
                // Check if event applies to this channel
                var channelIds = string.IsNullOrEmpty(boost.ApplicableChannels)
                    ? []
                    : boost.ApplicableChannels.Split(',').Select(ulong.Parse).ToList();

                if (channelIds.Count != 0 && !channelIds.Contains(channelId)) continue;

                // Check if event applies to user's roles
                var roleIds = string.IsNullOrEmpty(boost.ApplicableRoles)
                    ? []
                    : boost.ApplicableRoles.Split(',').Select(ulong.Parse).ToList();

                if (roleIds.Count == 0)
                {
                    multiplier *= boost.Multiplier;
                }
                else
                {
                    guild = client.GetGuild(guildId);
                    user = guild?.GetUser(userId);

                    if (user != null && user.Roles.Select(x => x.Id).Any(r => roleIds.Contains(r)))
                    {
                        multiplier *= boost.Multiplier;
                    }
                }
            }

            // Cache the result for 5 minutes
            await redisCache.StringSetAsync(cacheKey, multiplier, TimeSpan.FromMinutes(5));

            return multiplier;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating effective multiplier");
            return 1.0; // Default multiplier on error
        }
    }

    /// <summary>
    ///     Checks if a user can gain XP.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True if the user can gain XP, false otherwise.</returns>
    public async Task<bool> CanUserGainXpAsync(IGuildUser user, ulong channelId)
    {
        // Check exclusions cache first
        var exclusionCacheKey = $"xp:exclusion:{user.GuildId}:{user.Id}:{channelId}";

        await using var db = await dbFactory.CreateConnectionAsync();
        var isExcluded = false;

        // Check channel exclusion using LinqToDB
        var channelExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == user.GuildId &&
                       x.ItemId == channelId &&
                       x.ItemType == (int)ExcludedItemType.Channel);

        if (channelExcluded)
        {
            isExcluded = true;
        }
        else
        {
            // Check user exclusion using LinqToDB
            var userExcluded = await db.XpExcludedItems
                .AnyAsync(x => x.GuildId == user.GuildId &&
                           x.ItemId == user.Id &&
                           x.ItemType == (int)ExcludedItemType.User);

            if (userExcluded)
            {
                isExcluded = true;
            }
            else
            {
                // Check role exclusions using LinqToDB
                var excludedRoles = await db.XpExcludedItems
                    .Where(x => x.GuildId == user.GuildId && x.ItemType == (int)ExcludedItemType.Role)
                    .Select(x => x.ItemId)
                    .ToListAsync();

                if (user.RoleIds.Any(r => excludedRoles.Contains(r)))
                {
                    isExcluded = true;
                }
            }
        }

        // Cache the result for 15 minutes
        await redisCache.StringSetAsync(exclusionCacheKey, isExcluded, TimeSpan.FromMinutes(15));

        return !isExcluded;
    }

    /// <summary>
    ///     Checks and sets the XP cooldown for a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="cooldownSeconds">The cooldown in seconds.</param>
    /// <returns>True if the user is not on cooldown, false otherwise.</returns>
    public async Task<bool> CheckAndSetCooldownAsync(ulong guildId, ulong userId, int cooldownSeconds)
    {
        var cooldownKey = $"xp:cooldown:{guildId}:{userId}";
        var onCooldown = await redisCache.KeyExistsAsync(cooldownKey);

        if (onCooldown)
            return false;

        // Set cooldown
        await redisCache.StringSetAsync(cooldownKey, "1", TimeSpan.FromSeconds(cooldownSeconds));

        return true;
    }

    /// <summary>
    ///     Cleans up expired caches.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CleanupCachesAsync()
    {
        try
        {
            // Redis keyspace scan to find expired keys
            var server = dataCache.Redis.GetServer(dataCache.Redis.GetEndPoints().First());

            // Clean Redis cache for guilds that are no longer connected
            foreach (var key in server.Keys(pattern: "xp:guild_settings:*"))
            {
                var keyString = key.ToString();
                if (string.IsNullOrEmpty(keyString)) continue;

                var guildIdString = keyString.Split(':').LastOrDefault();
                if (string.IsNullOrEmpty(guildIdString) || !ulong.TryParse(guildIdString, out var guildId))
                    continue;

                if (client.GetGuild(guildId) == null)
                {
                    await redisCache.KeyDeleteAsync(key);
                }
            }

            // Clean up multiplier caches (they're short-lived)
            foreach (var key in server.Keys(pattern: "xp:multiplier:*"))
            {
                await redisCache.KeyExpireAsync(key, TimeSpan.FromMinutes(5));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning up caches");
        }
    }
}