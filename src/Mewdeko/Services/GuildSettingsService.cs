using System.Threading;
using DataModel;
using LinqToDB;
using Mewdeko.Common.Configs;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace Mewdeko.Services;

/// <summary>
///     Service responsible for managing Discord guild configurations using LinqToDB.
///     Provides methods for retrieving and updating guild settings with caching and context management.
/// </summary>
public class GuildSettingsService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly BotConfig botSettings;
    private readonly IMemoryCache cache;
    private readonly PerformanceMonitorService perfService;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> updateLocks; // Use ulong as key
    private readonly TimeSpan defaultCacheExpiration = TimeSpan.FromMinutes(30);
    private readonly TimeSpan slidingCacheExpiration = TimeSpan.FromMinutes(10);

    private static string GetPrefixCacheKey(ulong guildId)
    {
        return $"prefix_{guildId}";
    }

    private static string GetGuildConfigCacheKey(ulong guildId)
    {
        return $"guildconfig_{guildId}";
    }

    private static string GetReactionRolesCacheKey(ulong guildId)
    {
        return $"reactionroles_{guildId}";
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="GuildSettingsService" /> class.
    /// </summary>
    /// <param name="dbFactory">Factory for creating LinqToDB database connections.</param>
    /// <param name="botSettings">Service for accessing bot configuration settings.</param>
    /// <param name="memoryCache">Memory cache for storing frequently accessed guild settings.</param>
    /// <param name="perfService">Service for monitoring performance metrics.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
    public GuildSettingsService(
        IDataConnectionFactory dbFactory,
        BotConfig botSettings,
        IMemoryCache memoryCache,
        PerformanceMonitorService perfService)
    {
        this.dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        this.botSettings = botSettings ?? throw new ArgumentNullException(nameof(botSettings));
        cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        this.perfService = perfService ?? throw new ArgumentNullException(nameof(perfService));
        updateLocks = new ConcurrentDictionary<ulong, SemaphoreSlim>(); // Use ulong as key
    }

    /// <summary>
    ///     Retrieves the command prefix for a specified guild with caching.
    /// </summary>
    /// <param name="guild">The guild to get the prefix for. Can be null for default prefix.</param>
    /// <returns>The guild's custom prefix if set, otherwise the default bot prefix.</returns>
    public async Task<string> GetPrefix(IGuild? guild)
    {
        using (perfService.Measure(nameof(GetPrefix)))
        {
            if (guild == null)
                return botSettings.Prefix;

            var guildId = guild.Id;
            var cacheKey = GetPrefixCacheKey(guildId);

            if (cache.TryGetValue(cacheKey, out string? cachedPrefix) && cachedPrefix != null)
                return cachedPrefix;

            string? prefix = null;
            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                prefix = await db.GuildConfigs
                    .Where(x => x.GuildId == guildId)
                    .Select(x => x.Prefix)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve prefix for guild {GuildId}", guildId);
                return botSettings.Prefix;
            }

            var result = string.IsNullOrWhiteSpace(prefix) ? botSettings.Prefix : prefix;

            cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = defaultCacheExpiration, SlidingExpiration = slidingCacheExpiration
            });

            return result;
        }
    }

    /// <summary>
    ///     Sets a new command prefix for a specified guild and updates cache.
    /// </summary>
    /// <param name="guild">The guild to set the prefix for.</param>
    /// <param name="prefix">The new prefix to set.</param>
    /// <returns>The newly set prefix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either guild or prefix is null.</exception>
    public async Task<string> SetPrefix(IGuild guild, string prefix)
    {
        using (perfService.Measure(nameof(SetPrefix)))
        {
            ArgumentNullException.ThrowIfNull(guild);
            ArgumentNullException.ThrowIfNull(prefix);

            var guildId = guild.Id;
            var updateLock = updateLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));

            try
            {
                await updateLock.WaitAsync().ConfigureAwait(false);

                await using var db = await dbFactory.CreateConnectionAsync();
                await using var tx = await db.BeginTransactionAsync();
                try
                {
                    var config = await GetOrCreateGuildConfigInternal(db, guildId);
                    config.Prefix = prefix;
                    await db.UpdateAsync(config);
                    await tx.CommitAsync();

                    var cacheKey = GetPrefixCacheKey(guildId);
                    cache.Set(cacheKey, prefix, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = defaultCacheExpiration,
                        SlidingExpiration = slidingCacheExpiration
                    });
                    cache.Remove(GetGuildConfigCacheKey(guildId));

                    return prefix;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    Log.Error(ex, "Failed to set prefix for guild {GuildId} in transaction", guildId);
                    throw;
                }
            }
            finally
            {
                updateLock.Release();
            }
        }
    }

    /// <summary>
    ///     Gets the guild configuration for the specified guild ID with caching.
    ///     Retrieves from cache if available, otherwise fetches from DB (or creates if non-existent).
    /// </summary>
    /// <param name="guildId">The ID of the guild to get configuration for.</param>
    /// <param name="bypassCache">Set to true to force a database fetch, bypassing cache.</param>
    /// <returns>The guild configuration, or null if an error occurs during fetching or creation.</returns>
    public async Task<GuildConfig?> GetGuildConfig(ulong guildId, bool bypassCache = false)
    {
        using (perfService.Measure(nameof(GetGuildConfig)))
        {
            var cacheKey = GetGuildConfigCacheKey(guildId);

            if (!bypassCache && cache.TryGetValue(cacheKey, out GuildConfig? cachedConfig))
            {
                return cachedConfig;
            }

            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                var config = await GetOrCreateGuildConfigInternal(db, guildId);

                if (config != null)
                {
                    cache.Set(cacheKey, config, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = defaultCacheExpiration,
                        SlidingExpiration = slidingCacheExpiration
                    });
                }

                return config;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get or create guild config for {GuildId}", guildId);
                return null;
            }
        }
    }

    /// <summary>
    ///     Updates the guild configuration for a specified guild with proper cache invalidation.
    ///     Requires the GuildConfig object passed in to have the correct primary key (Id) set if it represents an existing
    ///     record.
    /// </summary>
    /// <param name="guildId">The ID of the guild to update configuration for.</param>
    /// <param name="toUpdate">The updated guild configuration object.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the GuildId in the passed object does not match the specified guildId.</exception>
    /// <exception cref="Exception">Thrown when the database update operation fails.</exception>
    public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate)
    {
        using (perfService.Measure(nameof(UpdateGuildConfig)))
        {
            if (toUpdate.GuildId != guildId)
            {
                throw new ArgumentException("GuildConfig GuildId does not match the provided guildId.",
                    nameof(toUpdate));
            }

            var updateLock = updateLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));

            try
            {
                await updateLock.WaitAsync().ConfigureAwait(false);

                await using var db = await dbFactory.CreateConnectionAsync();

                await using var tx = await db.BeginTransactionAsync();

                try
                {
                    var existingConfig = await db.GuildConfigs
                        .FirstOrDefaultAsync(x => x.Id == toUpdate.Id);

                    if (existingConfig == null)
                    {
                        Log.Warning("GuildConfig with ID {Id} not found in database", toUpdate.Id);
                        existingConfig = await db.GuildConfigs
                            .FirstOrDefaultAsync(x => x.GuildId == guildId);

                        if (existingConfig == null)
                        {
                            Log.Warning("No GuildConfig found for GuildId {GuildId}, creating new one", guildId);
                            existingConfig = await GetOrCreateGuildConfigInternal(db, guildId);
                        }
                    }

                    var properties = typeof(GuildConfig).GetProperties()
                        .Where(p => p.CanRead && p.CanWrite && p.Name != "Id" && p.Name != "DateAdded");

                    foreach (var prop in properties)
                    {
                        var oldValue = prop.GetValue(existingConfig);
                        var newValue = prop.GetValue(toUpdate);

                        if (!Equals(oldValue, newValue))
                        {
                            prop.SetValue(existingConfig, newValue);
                        }
                    }

                    existingConfig.CommandLogChannel = toUpdate.CommandLogChannel;

                    await db.UpdateAsync(existingConfig);

                    await tx.CommitAsync();

                    // Update cache with the database entity
                    var cacheKey = GetGuildConfigCacheKey(guildId);
                    cache.Set(cacheKey, existingConfig, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = defaultCacheExpiration,
                        SlidingExpiration = slidingCacheExpiration
                    });

                    // Update prefix cache if needed
                    if (!string.IsNullOrWhiteSpace(existingConfig.Prefix))
                    {
                        cache.Set(GetPrefixCacheKey(guildId), existingConfig.Prefix, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = defaultCacheExpiration,
                            SlidingExpiration = slidingCacheExpiration
                        });
                    }
                    else
                    {
                        cache.Remove(GetPrefixCacheKey(guildId));
                    }

                    cache.Remove(GetReactionRolesCacheKey(guildId));
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    Log.Error(ex, "Exception type: {Type}, Message: {Message}", ex.GetType().Name, ex.Message);
                    Log.Error(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                    Log.Error(ex, "Failed to update guild config for {GuildId} in transaction", guildId);
                    throw;
                }
            }
            finally
            {
                updateLock.Release();
            }
        }
    }

    /// <summary>
    ///     Clears the cache for a specific guild. Useful after manual database changes or significant configuration resets.
    /// </summary>
    /// <param name="guildId">The ID of the guild whose cache should be cleared.</param>
    public void ClearCacheForGuild(ulong guildId)
    {
        cache.Remove(GetPrefixCacheKey(guildId));
        cache.Remove(GetGuildConfigCacheKey(guildId));
        cache.Remove(GetReactionRolesCacheKey(guildId));
        Log.Information("Cache cleared for GuildId {GuildId}", guildId);
    }

    /// <summary>
    ///     Gets the reaction roles for a specific guild with caching, including associated role details.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get reaction roles for.</param>
    /// <param name="bypassCache">Set to true to force a database fetch, bypassing cache.</param>
    /// <returns>
    ///     A collection of reaction role messages for the guild, potentially with associated roles loaded, or null on
    ///     error.
    /// </returns>
    public async Task<HashSet<ReactionRoleMessage>?> GetReactionRoles(ulong guildId, bool bypassCache = false)
    {
        using (perfService.Measure(nameof(GetReactionRoles)))
        {
            var cacheKey = GetReactionRolesCacheKey(guildId);

            if (!bypassCache && cache.TryGetValue(cacheKey, out HashSet<ReactionRoleMessage>? cachedRoles))
            {
                return cachedRoles;
            }

            List<ReactionRoleMessage>? reactionRoleMessages = null;
            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                reactionRoleMessages = await db.ReactionRoleMessages
                    .LoadWithAsTable(rrm => rrm.ReactionRoles)
                    .Where(x => x.GuildId == guildId)
                    .OrderBy(rrm => rrm.Index)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get reaction roles for {GuildId}", guildId);
                return null;
            }

            var reactionRolesSet =
                new HashSet<ReactionRoleMessage>(reactionRoleMessages ?? new List<ReactionRoleMessage>());

            cache.Set(cacheKey, reactionRolesSet, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = defaultCacheExpiration, SlidingExpiration = slidingCacheExpiration
            });

            return reactionRolesSet;
        }
    }

    /// <summary>
    ///     Internal helper to get or create a guild configuration within a given DB connection.
    ///     Ensures configuration exists before returning.
    /// </summary>
    /// <param name="db">The active LinqToDB database connection (<see cref="MewdekoDb" />).</param>
    /// <param name="guildId">The ulong ID of the guild.</param>
    /// <returns>The existing or newly created guild configuration.</returns>
    private async Task<GuildConfig> GetOrCreateGuildConfigInternal(MewdekoDb db, ulong guildId)
    {
        var config = await db.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config == null)
        {
            Log.Information("Creating new GuildConfig for GuildId {GuildId}", guildId);
            config = new GuildConfig
            {
                GuildId = guildId, DateAdded = DateTime.UtcNow
            };

            var newId = await db.InsertWithInt32IdentityAsync(config);
            config.Id = newId;
        }

        return config;
    }
}