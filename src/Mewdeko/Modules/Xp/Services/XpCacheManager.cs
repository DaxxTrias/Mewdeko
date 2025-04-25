using System.Text.Json;
using DataModel;
using LinqToDB;
using Mewdeko.Database.DbContextStuff;
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

    // Memory caches for frequently accessed data
    private readonly ConcurrentDictionary<ulong, GuildXpSetting> guildSettingsCache = new();
    private readonly ConcurrentDictionary<(ulong, ulong), bool> exclusionCache = new();
    private readonly ConcurrentDictionary<string, double> multiplierCache = new();

    // Constants for Redis keys and cache parameters
    private const string RedisKeyPrefix = "xp:";
    private const string GuildSettingsKey = "guild_settings";
    private const string GuildUserKey = "guild_user";
    private const string CooldownKey = "cooldown";
    private const string ExclusionKey = "exclusion";
    private const string MultiplierKey = "multiplier";
    private const string FirstMsgKey = "first_msg";

    // Cache TTLs (can be tuned for different data types)
    private static readonly TimeSpan GuildSettingsTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan UserXpTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ExclusionTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MultiplierTtl = TimeSpan.FromMinutes(5);

    // Batch operation parameters
    private const int MaxBatchSize = 100;

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
        // Check memory cache first (we don't use this because we want to be distributed)
        var cacheKey = $"{RedisKeyPrefix}{GuildUserKey}:{guildId}:{userId}";

        // Try to get from Redis with one operation
        var redisData = await redisCache.StringGetAsync(cacheKey);
        if (redisData.HasValue)
        {
            try
            {
                var userData = JsonSerializer.Deserialize<GuildUserXp>(redisData);
                if (userData is not null)
                    return userData;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to deserialize user XP data from Redis");
            }
        }

        // Get from database using LinqToDB
        var userXp = await db.GuildUserXps
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (userXp == null)
        {
            // Create new user entry
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

        // Update caches - only cache here, don't duplicate Redis operations
        var serializedData = JsonSerializer.Serialize(userXp);
        await redisCache.StringSetAsync(cacheKey, serializedData, UserXpTtl);

        return userXp;
    }

    /// <summary>
    ///     Updates the user XP cache.
    /// </summary>
    /// <param name="userXp">The user XP record to cache.</param>
    public async void UpdateUserXpCacheAsync(GuildUserXp userXp)
    {
        var cacheKey = $"{RedisKeyPrefix}{GuildUserKey}:{userXp.GuildId}:{userXp.UserId}";

        try
        {
            // Update Redis cache
            var serializedData = JsonSerializer.Serialize(userXp);
            await redisCache.StringSetAsync(cacheKey, serializedData, UserXpTtl);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating XP cache for user {UserId} in guild {GuildId}",
                userXp.UserId, userXp.GuildId);
        }
    }

    /// <summary>
    ///     Gets the guild XP settings with multilevel caching.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild XP settings.</returns>
    public async Task<GuildXpSetting> GetGuildXpSettingsAsync(ulong guildId)
    {
        // Try memory cache first (fastest)
        if (guildSettingsCache.TryGetValue(guildId, out var cachedSettings))
        {
            return cachedSettings;
        }

        // Try Redis cache
        var cacheKey = $"{RedisKeyPrefix}{GuildSettingsKey}:{guildId}";
        var redisData = await redisCache.StringGetAsync(cacheKey);

        GuildXpSetting settings;
        if (redisData.HasValue)
        {
            try
            {
                settings = JsonSerializer.Deserialize<GuildXpSetting>(redisData);
                if (settings != null)
                {
                    // Update memory cache
                    guildSettingsCache[guildId] = settings;
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to deserialize guild settings from Redis");
            }
        }

        // Get from database using a single connection
        await using var db = await dbFactory.CreateConnectionAsync();

        settings = await db.GuildXpSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            // Create default settings
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

        // Update both caches
        var serializedData = JsonSerializer.Serialize(settings);
        await redisCache.StringSetAsync(cacheKey, serializedData, GuildSettingsTtl);
        guildSettingsCache[guildId] = settings;

        return settings;
    }

    /// <summary>
    ///     Updates the guild XP settings and refreshes caches.
    /// </summary>
    /// <param name="settings">The guild XP settings to update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateGuildXpSettingsAsync(GuildXpSetting settings)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Update using LinqToDB
        await db.UpdateAsync(settings);

        // Update both caches at once
        var cacheKey = $"{RedisKeyPrefix}{GuildSettingsKey}:{settings.GuildId}";
        var serializedData = JsonSerializer.Serialize(settings);

        // Update memory cache
        guildSettingsCache[settings.GuildId] = settings;

        // Update Redis cache
        await redisCache.StringSetAsync(cacheKey, serializedData, GuildSettingsTtl);
    }

    /// <summary>
    ///     Checks if a server is excluded from XP gain with memory caching.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True if the server is excluded, false otherwise.</returns>
    public async Task<bool> IsServerExcludedAsync(ulong guildId)
    {
        // Check memory cache for frequently accessed guilds
        if (guildSettingsCache.TryGetValue(guildId, out var settings))
        {
            return settings.XpGainDisabled;
        }

        // Load from database with caching
        settings = await GetGuildXpSettingsAsync(guildId);
        return settings.XpGainDisabled;
    }

    /// <summary>
    ///     Gets the effective XP multiplier for a user in a channel with caching.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The effective XP multiplier to apply.</returns>
    public async Task<double> GetEffectiveMultiplierAsync(ulong userId, ulong guildId, ulong channelId)
    {
        // Use composite key for multiplier cache
        var cacheKey = $"{RedisKeyPrefix}{MultiplierKey}:{guildId}:{userId}:{channelId}";
        var memoryCacheKey = $"{guildId}:{userId}:{channelId}";

        // Try memory cache first for hot multipliers
        if (multiplierCache.TryGetValue(memoryCacheKey, out var cachedMultiplier))
        {
            return cachedMultiplier;
        }

        // Try Redis cache
        var redisValue = await redisCache.StringGetAsync(cacheKey);
        if (redisValue.HasValue && double.TryParse(redisValue, out var multiplier))
        {
            // Update memory cache
            multiplierCache[memoryCacheKey] = multiplier;
            return multiplier;
        }

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get guild settings from cache
            var settings = await GetGuildXpSettingsAsync(guildId);
            multiplier = settings.XpMultiplier;

            // Apply channel multiplier if exists
            var channelMultiplier = await db.XpChannelMultipliers
                .FirstOrDefaultAsync(c => c.GuildId == guildId && c.ChannelId == channelId);

            if (channelMultiplier != null)
            {
                multiplier *= channelMultiplier.Multiplier;
            }

            // Get guild and user for role checks
            var guild = client.GetGuild(guildId);
            var user = guild?.GetUser(userId);

            if (user != null)
            {
                // Get user's role IDs
                var userRoleIds = user.Roles.Select(x => x.Id).ToList();

                if (userRoleIds.Count > 0)
                {
                    // Get role multipliers in a single query
                    var roleMultipliers = await db.XpRoleMultipliers
                        .Where(r => r.GuildId == guildId && userRoleIds.Contains(r.RoleId))
                        .ToListAsync();

                    if (roleMultipliers.Count != 0)
                    {
                        multiplier *= roleMultipliers.Max(r => r.Multiplier);
                    }
                }
            }

            // Apply active boost events in a single query
            var now = DateTime.UtcNow;
            var boostEvents = await db.XpBoostEvents
                .Where(b => b.GuildId == guildId && b.StartTime <= now && b.EndTime >= now)
                .ToListAsync();

            // Process all boost events
            foreach (var boost in boostEvents)
            {
                // Parse channel and role restrictions once
                var channelIds = string.IsNullOrEmpty(boost.ApplicableChannels)
                    ? new List<ulong>()
                    : boost.ApplicableChannels.Split(',').Select(ulong.Parse).ToList();

                var roleIds = string.IsNullOrEmpty(boost.ApplicableRoles)
                    ? new List<ulong>()
                    : boost.ApplicableRoles.Split(',').Select(ulong.Parse).ToList();

                // Check if this boost applies
                var applyBoost = !(channelIds.Count > 0 && !channelIds.Contains(channelId));

                // Channel restriction check

                // Role restriction check
                if (applyBoost && roleIds.Count > 0)
                {
                    if (user == null || !user.Roles.Select(x => x.Id).Any(r => roleIds.Contains(r)))
                    {
                        applyBoost = false;
                    }
                }

                // Apply multiplier if all checks passed
                if (applyBoost)
                {
                    multiplier *= boost.Multiplier;
                }
            }

            // Cache the result in both Redis and memory
            await redisCache.StringSetAsync(cacheKey, multiplier.ToString(), MultiplierTtl);
            multiplierCache[memoryCacheKey] = multiplier;

            return multiplier;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating effective multiplier");
            return 1.0; // Default multiplier on error
        }
    }

    /// <summary>
    ///     Checks if a user can gain XP with caching.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True if the user can gain XP, false otherwise.</returns>
    public async Task<bool> CanUserGainXpAsync(IGuildUser user, ulong channelId)
    {
        // Create composite cache key
        var exclusionCacheKey = $"{RedisKeyPrefix}{ExclusionKey}:{user.GuildId}:{user.Id}:{channelId}";
        var memoryCacheKey = (user.GuildId, user.Id);

        // Check memory cache first for hot users
        if (exclusionCache.TryGetValue(memoryCacheKey, out var isExcluded))
        {
            return !isExcluded;
        }

        // Check Redis cache
        var redisValue = await redisCache.StringGetAsync(exclusionCacheKey);
        if (redisValue.HasValue && bool.TryParse(redisValue, out isExcluded))
        {
            // Update memory cache
            exclusionCache[memoryCacheKey] = isExcluded;
            return !isExcluded;
        }

        // Need to check database
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check channel exclusion first (most granular)
        var channelExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == user.GuildId &&
                           x.ItemId == channelId &&
                           x.ItemType == (int)ExcludedItemType.Channel);

        if (channelExcluded)
        {
            // Cache the result
            await redisCache.StringSetAsync(exclusionCacheKey, "true", ExclusionTtl);
            exclusionCache[memoryCacheKey] = true;
            return false;
        }

        // Check user exclusion
        var userExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == user.GuildId &&
                           x.ItemId == user.Id &&
                           x.ItemType == (int)ExcludedItemType.User);

        if (userExcluded)
        {
            // Cache the result
            await redisCache.StringSetAsync(exclusionCacheKey, "true", ExclusionTtl);
            exclusionCache[memoryCacheKey] = true;
            return false;
        }

        // Check role exclusions
        var excludedRoles = await db.XpExcludedItems
            .Where(x => x.GuildId == user.GuildId && x.ItemType == (int)ExcludedItemType.Role)
            .Select(x => x.ItemId)
            .ToListAsync();

        if (user.RoleIds.Any(r => excludedRoles.Contains(r)))
        {
            // Cache the result
            await redisCache.StringSetAsync(exclusionCacheKey, "true", ExclusionTtl);
            exclusionCache[memoryCacheKey] = true;
            return false;
        }

        // User is not excluded
        await redisCache.StringSetAsync(exclusionCacheKey, "false", ExclusionTtl);
        exclusionCache[memoryCacheKey] = false;
        return true;
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
        var cooldownKey = $"{RedisKeyPrefix}{CooldownKey}:{guildId}:{userId}";

        // Use Redis SETNX for atomic check-and-set
        var notOnCooldown = await redisCache.StringSetAsync(
            cooldownKey,
            "1",
            TimeSpan.FromSeconds(cooldownSeconds),
            When.NotExists);

        return notOnCooldown;
    }

    /// <summary>
    ///     Cleans up expired caches in batches to reduce system impact.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CleanupCachesAsync()
    {
        try
        {
            // Get a Redis server for scanning
            var server = dataCache.Redis.GetServer(dataCache.Redis.GetEndPoints().First());

            // Clear memory caches periodically to prevent unbounded growth
            var memoryKeyCount = guildSettingsCache.Count;
            if (memoryKeyCount > 500)
            {
                guildSettingsCache.Clear();
                Log.Debug("Cleared {Count} items from guild settings memory cache", memoryKeyCount);
            }

            memoryKeyCount = exclusionCache.Count;
            if (memoryKeyCount > 1000)
            {
                exclusionCache.Clear();
                Log.Debug("Cleared {Count} items from exclusion memory cache", memoryKeyCount);
            }

            memoryKeyCount = multiplierCache.Count;
            if (memoryKeyCount > 2000)
            {
                multiplierCache.Clear();
                Log.Debug("Cleared {Count} items from multiplier memory cache", memoryKeyCount);
            }

            // Process Redis keys in batches to prevent long-running operations
            await ProcessDisconnectedGuildsAsync(server);
            await RefreshMultiplierCachesAsync(server);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning up caches");
        }
    }

    /// <summary>
    ///     Processes guilds that are no longer connected.
    /// </summary>
    private async Task ProcessDisconnectedGuildsAsync(IServer server)
    {
        // Build list of connected guild IDs for quick lookup
        var connectedGuildIds = new HashSet<ulong>(
            client.Guilds.Select(g => g.Id));

        // Process guild settings keys in batches
        var keysToDelete = new List<RedisKey>();
        var pattern = $"{RedisKeyPrefix}{GuildSettingsKey}:*";

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            var keyString = key.ToString();
            if (string.IsNullOrEmpty(keyString)) continue;

            var guildIdString = keyString.Split(':').LastOrDefault();
            if (string.IsNullOrEmpty(guildIdString) || !ulong.TryParse(guildIdString, out var guildId))
                continue;

            if (!connectedGuildIds.Contains(guildId))
            {
                keysToDelete.Add(key);

                // Remove from memory cache too
                guildSettingsCache.TryRemove(guildId, out _);

                // Process in batches
                if (keysToDelete.Count >= MaxBatchSize)
                {
                    await redisCache.KeyDeleteAsync(keysToDelete.ToArray());
                    Log.Debug("Deleted {Count} Redis keys for disconnected guilds", keysToDelete.Count);
                    keysToDelete.Clear();
                }
            }
        }

        // Delete any remaining keys
        if (keysToDelete.Count > 0)
        {
            await redisCache.KeyDeleteAsync(keysToDelete.ToArray());
            Log.Debug("Deleted {Count} Redis keys for disconnected guilds", keysToDelete.Count);
        }
    }

    /// <summary>
    ///     Refreshes multiplier caches to ensure they don't expire.
    /// </summary>
    private async Task RefreshMultiplierCachesAsync(IServer server)
    {
        // Process multiplier keys to ensure they have proper expiration
        var keysToRefresh = new List<RedisKey>();
        var pattern = $"{RedisKeyPrefix}{MultiplierKey}:*";

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            keysToRefresh.Add(key);

            // Process in batches
            if (keysToRefresh.Count >= MaxBatchSize)
            {
                var tasks = keysToRefresh.Select(key => redisCache.KeyExpireAsync(key, MultiplierTtl)).ToArray();
                await Task.WhenAll(tasks);

                keysToRefresh.Clear();
            }
        }

        // Process any remaining keys
        if (keysToRefresh.Count > 0)
        {
            var tasks = keysToRefresh.Select(key => redisCache.KeyExpireAsync(key, MultiplierTtl)).ToArray();
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    ///     Clears all XP-related caches for a specific guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ClearGuildXpCachesAsync(ulong guildId)
    {
        try
        {
            // Clear memory caches first
            guildSettingsCache.TryRemove(guildId, out _);

            // Clear relevant exclusion entries
            var exclusionKeysToRemove = exclusionCache.Keys
                .Where(k => k.Item1 == guildId)
                .ToList();

            foreach (var key in exclusionKeysToRemove)
            {
                exclusionCache.TryRemove(key, out _);
            }

            // Clear relevant multiplier entries
            var multiplierKeysToRemove = multiplierCache.Keys
                .Where(k => k.StartsWith($"{guildId}:"))
                .ToList();

            foreach (var key in multiplierKeysToRemove)
            {
                multiplierCache.TryRemove(key, out _);
            }

            // Get the Redis server instance
            var server = dataCache.Redis.GetServer(dataCache.Redis.GetEndPoints().First());

            // Define patterns for Redis keys to remove
            var patterns = new[]
            {
                $"{RedisKeyPrefix}{GuildUserKey}:{guildId}:*", $"{RedisKeyPrefix}{GuildSettingsKey}:{guildId}",
                $"{RedisKeyPrefix}{ExclusionKey}:{guildId}:*", $"{RedisKeyPrefix}{MultiplierKey}:{guildId}:*",
                $"{RedisKeyPrefix}{CooldownKey}:{guildId}:*", $"{RedisKeyPrefix}{FirstMsgKey}:{guildId}:*"
            };

            // Gather all keys to delete
            var redisKeysToDelete = new List<RedisKey>();

            foreach (var pattern in patterns)
            {
                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    redisKeysToDelete.Add(key);

                    // Delete in batches to avoid large operations
                    if (redisKeysToDelete.Count >= MaxBatchSize)
                    {
                        await redisCache.KeyDeleteAsync(redisKeysToDelete.ToArray());
                        Log.Debug("Deleted {Count} Redis keys for guild {GuildId}",
                            redisKeysToDelete.Count, guildId);
                        redisKeysToDelete.Clear();
                    }
                }
            }

            // Delete any remaining keys
            if (redisKeysToDelete.Count > 0)
            {
                await redisCache.KeyDeleteAsync(redisKeysToDelete.ToArray());
                Log.Debug("Deleted {Count} remaining Redis keys for guild {GuildId}",
                    redisKeysToDelete.Count, guildId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error clearing XP caches for guild {GuildId}", guildId);
        }
    }
}