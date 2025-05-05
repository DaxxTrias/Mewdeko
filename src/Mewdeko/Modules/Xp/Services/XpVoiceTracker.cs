using System.Threading;
using DataModel;
using LinqToDB;
using Mewdeko.Modules.Xp.Models;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Manages voice channel XP tracking.
/// </summary>
public class XpVoiceTracker : INService, IDisposable
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly XpCacheManager cacheManager;

    // Track active voice sessions
    private readonly ConcurrentDictionary<(ulong UserId, ulong ChannelId), VoiceSession> voiceSessions = new();

    private readonly MemoryCache exclusionCache;
    private readonly MemoryCacheOptions cacheOptions = new()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(5)
    };

    // Cache for guild settings
    private readonly ConcurrentDictionary<ulong, (bool IsExcluded, DateTime LastChecked)> guildExclusionCache = new();
    private readonly ConcurrentDictionary<ulong, (GuildXpSetting Settings, DateTime LastChecked)> guildSettingsCache = new();

    // Track channel eligibility (requires at least 2 users)
    private readonly ConcurrentDictionary<(ulong GuildId, ulong ChannelId), (bool IsEligible, DateTime LastChecked)> channelEligibilityCache = new();

    // Cache TTLs
    private readonly TimeSpan guildCacheTtl = TimeSpan.FromMinutes(15);
    private readonly TimeSpan exclusionCacheTtl = TimeSpan.FromMinutes(5);
    private readonly TimeSpan eligibilityCacheTtl = TimeSpan.FromMinutes(2);

    // Constants
    private const int MinUsersForXp = 2;

    // Timers for maintenance operations
    private readonly Timer validationTimer;
    private readonly Timer cleanupTimer;
    private readonly Timer diagnosticsTimer;

    // Session validation interval
    private readonly TimeSpan validationInterval = TimeSpan.FromMinutes(5);

    // Processing flag to prevent overlapping operations
    private int isProcessing;

    /// <summary>
    ///     Represents a voice session for a user in a voice channel.
    /// </summary>
    private class VoiceSession
    {
        /// <summary>
        ///     Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        ///     Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        ///     Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        ///     Gets or sets the join time.
        /// </summary>
        public DateTime JoinTime { get; set; }

        /// <summary>
        ///     Gets or sets the total eligible duration.
        /// </summary>
        public TimeSpan TotalEligibleDuration { get; set; } = TimeSpan.Zero;

        /// <summary>
        ///     Gets or sets the last eligibility start time.
        /// </summary>
        public DateTime? LastEligibilityStart { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the session is currently eligible.
        /// </summary>
        public bool CurrentlyEligible { get; set; }

        /// <summary>
        ///     Gets or sets the last time the eligibility state changed.
        /// </summary>
        public DateTime LastEligibilityChange { get; set; }

        /// <summary>
        ///     Calculates the total eligible duration so far.
        /// </summary>
        /// <param name="referenceTime">The reference time to calculate up to.</param>
        /// <returns>The total eligible duration.</returns>
        public TimeSpan GetEligibleDuration(DateTime referenceTime)
        {
            if (!CurrentlyEligible) return TotalEligibleDuration;
            if (!LastEligibilityStart.HasValue) return TotalEligibleDuration;

            return TotalEligibleDuration + (referenceTime - LastEligibilityStart.Value);
        }

        /// <summary>
        ///     Starts tracking eligible time.
        /// </summary>
        /// <param name="timestamp">The timestamp when eligibility started.</param>
        public void StartEligiblePeriod(DateTime timestamp)
        {
            if (CurrentlyEligible) return;

            CurrentlyEligible = true;
            LastEligibilityChange = timestamp;
            LastEligibilityStart = timestamp;
        }

        /// <summary>
        ///     Ends current eligible period.
        /// </summary>
        /// <param name="timestamp">The timestamp when eligibility ended.</param>
        public void EndEligiblePeriod(DateTime timestamp)
        {
            if (!CurrentlyEligible || !LastEligibilityStart.HasValue) return;

            TotalEligibleDuration += (timestamp - LastEligibilityStart.Value);
            CurrentlyEligible = false;
            LastEligibilityChange = timestamp;
            LastEligibilityStart = null;
        }
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpVoiceTracker" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="cacheManager">The cache manager.</param>
    public XpVoiceTracker(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        XpCacheManager cacheManager)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.cacheManager = cacheManager;

        // Initialize memory cache
        exclusionCache = new MemoryCache(cacheOptions);

        // Initialize validation timer (5 minutes)
        validationTimer = new Timer(ValidateActiveSessions, null,
            TimeSpan.FromMinutes(1), validationInterval);

        // Initialize cleanup timer to run every 20 minutes
        cleanupTimer = new Timer(CleanupStaleData, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(20));

        // Initialize diagnostics timer to log stats every hour
        diagnosticsTimer = new Timer(_ => LogDiagnostics(), null,
            TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

        Log.Information("Voice XP Tracker initialized with optimized memory usage");
    }

    /// <summary>
    ///     Handles voice state updates for voice XP tracking.
    /// </summary>
    /// <param name="user">The user whose voice state changed.</param>
    /// <param name="before">The previous voice state.</param>
    /// <param name="after">The new voice state.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleVoiceStateUpdate(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user is not SocketGuildUser guildUser || guildUser.IsBot)
            return;

        try
        {
            var now = DateTime.UtcNow;
            var guildId = guildUser.Guild.Id;
            var userId = guildUser.Id;

            // Get guild settings
            var settings = await GetGuildSettingsAsync(guildId);

            // Skip if voice XP is disabled
            if (settings.XpGainDisabled || settings.VoiceXpPerMinute <= 0)
                return;

            // User left voice channel
            if (before.VoiceChannel != null &&
                (after.VoiceChannel == null || after.VoiceChannel.Id != before.VoiceChannel.Id))
            {
                var channelId = before.VoiceChannel.Id;
                var key = (userId, channelId);

                if (voiceSessions.TryRemove(key, out var session))
                {
                    // If session exists, end eligibility period and calculate XP
                    if (session.CurrentlyEligible)
                    {
                        session.EndEligiblePeriod(now);
                    }

                    await CalculateAndAwardXp(guildUser, before.VoiceChannel, session);
                }
            }

            // User joined voice channel
            if (after.VoiceChannel != null &&
                (before.VoiceChannel == null || before.VoiceChannel.Id != after.VoiceChannel.Id))
            {
                await StartVoiceSession(guildUser, after.VoiceChannel, now);
            }

            // User remained in same channel but state changed (mute/deafen)
            // This affects eligibility
            if (after.VoiceChannel != null && before.VoiceChannel != null &&
                after.VoiceChannel.Id == before.VoiceChannel.Id &&
                (IsParticipatingInVoice(before) != IsParticipatingInVoice(after)))
            {
                await UpdateSessionEligibility(guildUser, after.VoiceChannel, now);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling voice state update for {UserId} in {GuildId}",
                user.Id, guildUser.Guild.Id);
        }
    }

    /// <summary>
    ///     Scans a guild's voice channels and sets up voice sessions.
    /// </summary>
    /// <param name="guild">The guild to scan.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ScanGuildVoiceChannels(SocketGuild guild)
    {
        try
        {
            var now = DateTime.UtcNow;
            var guildId = guild.Id;

            // Get guild settings
            var settings = await GetGuildSettingsAsync(guildId);

            // Skip if voice XP is disabled
            if (settings.XpGainDisabled || settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
                return;

            // Process all voice channels
            foreach (var channel in guild.VoiceChannels)
            {
                // Check if channel has enough users for XP
                var eligibleUsers = channel.Users.Where(u => !u.IsBot && IsParticipatingInVoice(u)).ToList();
                var isEligible = eligibleUsers.Count >= MinUsersForXp;

                // Update channel eligibility cache
                channelEligibilityCache[(guildId, channel.Id)] = (isEligible, now);

                // Skip if channel doesn't meet minimum requirements
                if (!isEligible)
                    continue;

                // Check channel exclusion
                if (await IsChannelExcludedAsync(guildId, channel.Id))
                    continue;

                // Start sessions for eligible users
                foreach (var user in eligibleUsers)
                {
                    if (await IsUserExcludedAsync(guildId, user.Id))
                        continue;

                    await StartVoiceSession(user, channel, now);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scanning voice channels for guild {GuildId}", guild.Id);
        }
    }

    /// <summary>
    ///     Starts a voice session for a user or updates an existing one.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="voiceChannel">The voice channel.</param>
    /// <param name="timestamp">The current timestamp.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task StartVoiceSession(SocketGuildUser user, SocketVoiceChannel voiceChannel, DateTime timestamp)
    {
        var guildId = user.Guild.Id;
        var userId = user.Id;
        var channelId = voiceChannel.Id;

        try
        {
            // Check if user is excluded
            if (await IsUserExcludedAsync(guildId, userId))
                return;

            // Check if channel is excluded
            if (await IsChannelExcludedAsync(guildId, channelId))
                return;

            // Get or create session
            var key = (userId, channelId);
            var session = voiceSessions.GetOrAdd(key, _ => new VoiceSession
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                JoinTime = timestamp,
                CurrentlyEligible = false,
                LastEligibilityChange = timestamp
            });

            // Check channel eligibility (requires 2+ users)
            var isEligible = await IsChannelEligibleAsync(guildId, channelId, voiceChannel);

            // Start eligible period if conditions are met
            if (isEligible && IsParticipatingInVoice(user))
            {
                session.StartEligiblePeriod(timestamp);
            }

            // Update session in dictionary
            voiceSessions[key] = session;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting voice session for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }

    /// <summary>
    ///     Updates the eligibility status of an existing session.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="voiceChannel">The voice channel.</param>
    /// <param name="timestamp">The current timestamp.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task UpdateSessionEligibility(SocketGuildUser user, SocketVoiceChannel voiceChannel, DateTime timestamp)
    {
        var guildId = user.Guild.Id;
        var userId = user.Id;
        var channelId = voiceChannel.Id;
        var key = (userId, channelId);

        // Only proceed if we have an existing session
        if (!voiceSessions.TryGetValue(key, out var session))
            return;

        try
        {
            // Check overall channel eligibility
            var isChannelEligible = await IsChannelEligibleAsync(guildId, channelId, voiceChannel);

            // Is this user participating
            var isUserParticipating = IsParticipatingInVoice(user);

            // Update session eligibility based on current conditions
            var shouldBeEligible = isChannelEligible && isUserParticipating;

            switch (shouldBeEligible)
            {
                case true when !session.CurrentlyEligible:
                    // Start new eligible period
                    session.StartEligiblePeriod(timestamp);
                    break;
                case false when session.CurrentlyEligible:
                    // End current eligible period
                    session.EndEligiblePeriod(timestamp);
                    break;
            }

            // Update session in dictionary
            voiceSessions[key] = session;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating session eligibility for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }

    /// <summary>
    ///     Calculates and awards XP when a user leaves a voice channel.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="voiceChannel">The voice channel.</param>
    /// <param name="session">The voice session.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CalculateAndAwardXp(SocketGuildUser user, SocketVoiceChannel voiceChannel, VoiceSession session)
    {
        var guildId = user.Guild.Id;
        var userId = user.Id;
        var channelId = voiceChannel.Id;

        try
        {
            // Get settings
            var settings = await GetGuildSettingsAsync(guildId);

            // Skip if voice XP is disabled
            if (settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
                return;

            // Calculate total eligible duration
            var totalEligibleDuration = session.GetEligibleDuration(DateTime.UtcNow);

            // Skip if duration is too short
            if (totalEligibleDuration.TotalMinutes < 0.25) // 15 seconds minimum
                return;

            // Cap at maximum timeout to prevent abuse
            var cappedMinutes = Math.Min(totalEligibleDuration.TotalMinutes, settings.VoiceXpTimeout);

            // Calculate XP to award
            var baseXpPerMinute = Math.Min(settings.VoiceXpPerMinute, XpService.MaxVoiceXpPerMinute);
            var xpAmount = (int)(baseXpPerMinute * cappedMinutes);

            switch (xpAmount)
            {
                // Skip if no XP to award
                case <= 0:
                    return;
                // Apply multipliers for significant XP amounts
                case >= 5:
                    xpAmount = (int)(xpAmount *
                                     await cacheManager.GetEffectiveMultiplierAsync(userId, guildId, channelId));
                    break;
            }

            // Skip if still no XP after multipliers
            if (xpAmount <= 0)
                return;

            // Get background processor from XpService
            var backgroundProcessor = XpService.Instance?.GetBackgroundProcessor();
            if (backgroundProcessor == null)
                return;

            // Queue XP gain
            backgroundProcessor.QueueXpGain(
                guildId,
                userId,
                xpAmount,
                channelId,
                XpSource.Voice
            );

            // Log significant XP awards
            if (xpAmount > 50)
            {
                Log.Information("Awarded {XpAmount} voice XP to user {UserId} in guild {GuildId} for {Minutes:F1} minutes",
                    xpAmount, userId, guildId, cappedMinutes);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating XP for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }

    /// <summary>
    ///     Periodically validates active sessions to ensure channel eligibility is current.
    /// </summary>
    /// <param name="state">The state object.</param>
    private async void ValidateActiveSessions(object state)
    {
        if (Interlocked.Exchange(ref isProcessing, 1) != 0)
            return;

        var startTime = DateTime.UtcNow;
        var sessionsUpdated = 0;

        try
        {
            // Skip if no sessions to validate
            if (voiceSessions.IsEmpty)
                return;

            // Process in batches to avoid memory spikes
            var batchesProcessed = 0;

            var sessionsByChannel = voiceSessions.Values
                .GroupBy(s => (s.GuildId, s.ChannelId))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var ((guildId, channelId), sessions) in sessionsByChannel)
            {
                try
                {
                    // Skip if guild is excluded
                    if (IsGuildExcluded(guildId))
                        continue;

                    // Skip if channel is excluded
                    if (await IsChannelExcludedAsync(guildId, channelId))
                        continue;

                    // Get guild and channel
                    var guild = client.GetGuild(guildId);
                    var channel = guild?.GetVoiceChannel(channelId);

                    if (guild == null || channel == null)
                        continue;

                    // Check channel eligibility (need at least 2 participating users)
                    var eligibleUserCount = channel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u));
                    var isEligible = eligibleUserCount >= MinUsersForXp;

                    // Update channel eligibility cache
                    channelEligibilityCache[(guildId, channelId)] = (isEligible, startTime);

                    // Skip if no change in eligibility
                    var wasEligible = sessions.Any(s => s.CurrentlyEligible);
                    if (isEligible == wasEligible)
                        continue;

                    // Update all sessions for this channel
                    foreach (var session in sessions)
                    {
                        try
                        {
                            var user = guild.GetUser(session.UserId);

                            // Skip if user no longer exists or isn't in this channel
                            if (user == null || user.VoiceChannel?.Id != channelId)
                                continue;

                            switch (isEligible)
                            {
                                // Update session eligibility
                                case true when !session.CurrentlyEligible && IsParticipatingInVoice(user):
                                    session.StartEligiblePeriod(startTime);
                                    voiceSessions[(session.UserId, session.ChannelId)] = session;
                                    sessionsUpdated++;
                                    break;
                                case false when session.CurrentlyEligible:
                                    session.EndEligiblePeriod(startTime);
                                    voiceSessions[(session.UserId, session.ChannelId)] = session;
                                    sessionsUpdated++;
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error updating session for user {UserId} in guild {GuildId}",
                                session.UserId, guildId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error validating channel {ChannelId} in guild {GuildId}",
                        channelId, guildId);
                }

                // Add a delay between batches to reduce CPU spikes
                batchesProcessed++;
                if (batchesProcessed % 5 == 0)
                {
                    await Task.Delay(50);
                }
            }

            if (sessionsUpdated > 0)
            {
                var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                Log.Debug("Updated eligibility for {Count} voice sessions in {Time:F1}ms",
                    sessionsUpdated, elapsedMs);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error validating voice sessions");
        }
        finally
        {
            Interlocked.Exchange(ref isProcessing, 0);
        }
    }

    /// <summary>
    ///     Cleans up stale data to prevent memory leaks.
    /// </summary>
    /// <param name="state">The state object.</param>
    private async void CleanupStaleData(object state)
    {
        var now = DateTime.UtcNow;
        var sessionsRemoved = 0;

        try
        {
            // Take snapshot of session keys to avoid modification during iteration
            var sessionKeys = voiceSessions.Keys.ToList();

            // Process in batches to reduce CPU spikes
            const int batchSize = 100;
            for (var i = 0; i < sessionKeys.Count; i += batchSize)
            {
                var batchKeys = sessionKeys.Skip(i).Take(batchSize).ToList();
                foreach (var key in batchKeys)
                {
                    if (!voiceSessions.TryGetValue(key, out var session))
                        continue;

                    try
                    {
                        var guild = client.GetGuild(session.GuildId);
                        if (guild == null)
                        {
                            voiceSessions.TryRemove(key, out _);
                            sessionsRemoved++;
                            continue;
                        }

                        var user = guild.GetUser(session.UserId);
                        if (user == null)
                        {
                            voiceSessions.TryRemove(key, out _);
                            sessionsRemoved++;
                            continue;
                        }

                        // Check if user is still in this voice channel
                        if (user.VoiceChannel == null || user.VoiceChannel.Id != session.ChannelId)
                        {
                            voiceSessions.TryRemove(key, out _);
                            sessionsRemoved++;
                        }
                    }
                    catch
                    {
                        // If any error occurs, remove the key to be safe
                        voiceSessions.TryRemove(key, out _);
                        sessionsRemoved++;
                    }
                }

                // Add a delay between batches
                await Task.Delay(50);
            }

            // Clear expired cache entries
            ClearExpiredCaches(now);

            if (sessionsRemoved > 0)
            {
                Log.Information("Voice tracker cleanup: removed {Count} stale sessions, " +
                               "active sessions: {Active}",
                    sessionsRemoved, voiceSessions.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in voice tracker cleanup");
        }
    }

    /// <summary>
    ///     Clears expired cache entries.
    /// </summary>
    /// <param name="now">The current time.</param>
    private void ClearExpiredCaches(DateTime now)
    {
        // Clear expired guild cache entries
        var expiredGuilds = guildExclusionCache
            .Where(kvp => (now - kvp.Value.LastChecked) > guildCacheTtl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var guildId in expiredGuilds)
        {
            guildExclusionCache.TryRemove(guildId, out _);
        }

        expiredGuilds = guildSettingsCache
            .Where(kvp => (now - kvp.Value.LastChecked) > guildCacheTtl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var guildId in expiredGuilds)
        {
            guildSettingsCache.TryRemove(guildId, out _);
        }

        // Clear expired eligibility cache entries
        var expiredEligibility = channelEligibilityCache
            .Where(kvp => (now - kvp.Value.LastChecked) > eligibilityCacheTtl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredEligibility)
        {
            channelEligibilityCache.TryRemove(key, out _);
        }

        // Compact the memory cache
        exclusionCache?.Compact(100);
    }

    /// <summary>
    ///     Gets guild settings with local caching to reduce Redis calls.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild XP settings.</returns>
    private async Task<GuildXpSetting> GetGuildSettingsAsync(ulong guildId)
    {
        var now = DateTime.UtcNow;

        // Check local cache first
        if (guildSettingsCache.TryGetValue(guildId, out var cacheEntry) &&
            (now - cacheEntry.LastChecked) < guildCacheTtl)
        {
            return cacheEntry.Settings;
        }

        // Get from cache manager
        var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);

        // Update local cache
        guildSettingsCache[guildId] = (settings, now);

        // Also update exclusion cache
        guildExclusionCache[guildId] = (settings.XpGainDisabled, now);

        return settings;
    }

    /// <summary>
    ///     Checks if a guild is excluded from XP gain using local cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True if the guild is excluded.</returns>
    private bool IsGuildExcluded(ulong guildId)
    {
        var now = DateTime.UtcNow;

        // Check local cache first
        if (guildExclusionCache.TryGetValue(guildId, out var cacheEntry) &&
            (now - cacheEntry.LastChecked) < guildCacheTtl)
        {
            return cacheEntry.IsExcluded;
        }

        // Not in cache, we'll need to get settings - but for now assume not excluded
        // to avoid blocking. It will be updated on next settings fetch.
        return false;
    }

    /// <summary>
    ///     Checks if a channel is excluded.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>A task representing the asynchronous operation with a boolean result.</returns>
    private async Task<bool> IsChannelExcludedAsync(ulong guildId, ulong channelId)
    {
        // Try to get from memory cache
        var cacheKey = $"channel:{guildId}:{channelId}";
        if (exclusionCache.TryGetValue(cacheKey, out bool isExcluded))
        {
            return isExcluded;
        }

        // Check from database
        await using var db = await dbFactory.CreateConnectionAsync();
        isExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId &&
                          x.ItemId == channelId &&
                          x.ItemType == (int)ExcludedItemType.Channel);

        // Cache the result
        var options = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetAbsoluteExpiration(exclusionCacheTtl);

        exclusionCache.Set(cacheKey, isExcluded, options);

        return isExcluded;
    }

    /// <summary>
    ///     Checks if a user is excluded.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>A task representing the asynchronous operation with a boolean result.</returns>
    private async Task<bool> IsUserExcludedAsync(ulong guildId, ulong userId)
    {
        // Try to get from memory cache
        var cacheKey = $"user:{guildId}:{userId}";
        if (exclusionCache.TryGetValue(cacheKey, out bool isExcluded))
        {
            return isExcluded;
        }

        // Check database for user exclusion
        await using var db = await dbFactory.CreateConnectionAsync();

        // Direct user exclusion
        isExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId &&
                          x.ItemId == userId &&
                          x.ItemType == (int)ExcludedItemType.User);

        if (!isExcluded)
        {
            // User could be excluded by role
            var user = client.GetGuild(guildId)?.GetUser(userId);
            if (user != null)
            {
                var excludedRoles = await db.XpExcludedItems
                    .Where(x => x.GuildId == guildId && x.ItemType == (int)ExcludedItemType.Role)
                    .Select(x => x.ItemId)
                    .ToListAsync();

                isExcluded = user.Roles.Any(r => excludedRoles.Contains(r.Id));
            }
        }

        // Cache the result
        var options = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetAbsoluteExpiration(exclusionCacheTtl);

        exclusionCache.Set(cacheKey, isExcluded, options);

        return isExcluded;
    }

    /// <summary>
    ///     Checks if a channel is eligible for XP (has at least 2 users).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="voiceChannel">The voice channel, if available.</param>
    /// <returns>A task representing the asynchronous operation with a boolean result.</returns>
    private async Task<bool> IsChannelEligibleAsync(ulong guildId, ulong channelId, SocketVoiceChannel? voiceChannel = null)
    {
        var now = DateTime.UtcNow;

        // Check local cache for recent results
        if (channelEligibilityCache.TryGetValue((guildId, channelId), out var cacheEntry) &&
            (now - cacheEntry.LastChecked) < eligibilityCacheTtl)
        {
            return cacheEntry.IsEligible;
        }

        // If we have the channel object, check it directly
        if (voiceChannel != null)
        {
            var isEligible = voiceChannel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u)) >= MinUsersForXp;
            channelEligibilityCache[(guildId, channelId)] = (isEligible, now);
            return isEligible;
        }

        // Otherwise, get the channel and check
        var guild = client.GetGuild(guildId);
        var channel = guild?.GetVoiceChannel(channelId);

        if (channel == null)
        {
            channelEligibilityCache[(guildId, channelId)] = (false, now);
            return false;
        }

        var eligibleCount = channel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u));
        var eligible = eligibleCount >= MinUsersForXp;

        channelEligibilityCache[(guildId, channelId)] = (eligible, now);
        return eligible;
    }

    /// <summary>
    ///     Determines if a user is actively participating in a voice channel.
    /// </summary>
    /// <param name="user">The voice state user.</param>
    /// <returns>True if the user is participating, false otherwise.</returns>
    private static bool IsParticipatingInVoice(IVoiceState user)
    {
        // Check user's voice state for participation
        return !user.IsDeafened && !user.IsMuted && !user.IsSelfDeafened && !user.IsSelfMuted;
    }

    /// <summary>
    ///     Logs diagnostic information about the voice tracker.
    /// </summary>
    private void LogDiagnostics()
    {
        Log.Information("XpVoiceTracker stats: Active sessions: {SessionCount}, " +
                       "Guild cache: {GuildCount}, Channel eligibility cache: {ChannelCount}, " +
                       "Exclusion cache: {ExclusionCount}",
            voiceSessions.Count,
            guildSettingsCache.Count,
            channelEligibilityCache.Count,
            exclusionCache.Count);
    }

    /// <summary>
    ///     Disposes resources used by the voice tracker.
    /// </summary>
    public void Dispose()
    {
        validationTimer?.Dispose();
        cleanupTimer?.Dispose();
        diagnosticsTimer?.Dispose();
        voiceSessions.Clear();
        exclusionCache.Dispose();
        guildExclusionCache.Clear();
        guildSettingsCache.Clear();
        channelEligibilityCache.Clear();
    }
}