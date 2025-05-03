using System.Threading;
using DataModel;
using LinqToDB;
using Mewdeko.Modules.Xp.Models;
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

    // Track active voice sessions without processing them periodically
    private readonly ConcurrentDictionary<string, VoiceSession> voiceSessions = new();

    // Local caches to reduce database and Redis calls
    private readonly ConcurrentDictionary<ulong, (bool IsExcluded, DateTime LastChecked)> guildExclusionCache = new();
    private readonly ConcurrentDictionary<ulong, (GuildXpSetting Settings, DateTime LastChecked)> guildSettingsCache = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong ChannelId), (bool IsExcluded, DateTime LastChecked)> channelExclusionCache = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), (bool IsExcluded, DateTime LastChecked)> userExclusionCache = new();

    // Track channel eligibility (requires at least 2 users)
    private readonly ConcurrentDictionary<(ulong GuildId, ulong ChannelId), (bool IsEligible, DateTime LastChecked)> channelEligibilityCache = new();

    // Cache TTLs
    private readonly TimeSpan guildCacheTtl = TimeSpan.FromMinutes(30);
    private readonly TimeSpan exclusionCacheTtl = TimeSpan.FromMinutes(15);
    private readonly TimeSpan eligibilityCacheTtl = TimeSpan.FromMinutes(5);

    // Constants
    private const int MinUsersForXp = 2;

    // Timer for validating channel eligibility periodically (very infrequently)
    private readonly Timer validationTimer;
    private readonly Timer cleanupTimer;

    // Session validation interval (10 minutes)
    private readonly TimeSpan validationInterval = TimeSpan.FromMinutes(10);

    private class VoiceSession
    {
        // Basic session info
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public string SessionKey { get; set; }
        public DateTime JoinTime { get; set; }

        // Track eligible time periods
        private List<(DateTime Start, DateTime? End)> EligiblePeriods { get; set; } = new();

        // Current eligibility state
        public bool CurrentlyEligible { get; set; }
        public DateTime LastEligibilityChange { get; set; }

        // Calculate total eligible duration so far
        public TimeSpan GetEligibleDuration(DateTime referenceTime)
        {
            var totalTicks = 0L;

            // Add up completed periods
            foreach (var (start, end) in EligiblePeriods.Where(p => p.End.HasValue))
            {
                totalTicks += (end.Value - start).Ticks;
            }

            // Add current period if eligible
            if (!CurrentlyEligible) return new TimeSpan(totalTicks);
            var periodStart = EligiblePeriods.LastOrDefault().End ?? LastEligibilityChange;
            totalTicks += (referenceTime - periodStart).Ticks;

            return new TimeSpan(totalTicks);
        }

        // Start tracking eligible time
        public void StartEligiblePeriod(DateTime timestamp)
        {
            if (CurrentlyEligible) return;
            CurrentlyEligible = true;
            LastEligibilityChange = timestamp;
            EligiblePeriods.Add((timestamp, null));
        }

        // End current eligible period
        public void EndEligiblePeriod(DateTime timestamp)
        {
            if (!CurrentlyEligible || EligiblePeriods.Count <= 0) return;
            CurrentlyEligible = false;
            LastEligibilityChange = timestamp;

            // Update the latest period's end time
            var lastIdx = EligiblePeriods.Count - 1;
            var lastPeriod = EligiblePeriods[lastIdx];
            EligiblePeriods[lastIdx] = (lastPeriod.Start, timestamp);
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

        // Initialize a very infrequent validation timer (10 minutes)
        // This only validates channel eligibility for existing sessions
        validationTimer = new Timer(ValidateActiveSessions, null,
            TimeSpan.FromMinutes(1), validationInterval);

        // Initialize cleanup timer to run every hour
        cleanupTimer = new Timer(CleanupStaleData, null,
            TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        Log.Information("Voice XP Tracker initialized in exit-only mode for maximum efficiency");
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

            // Quick check for server exclusion from local cache first
            if (IsGuildExcluded(guildId))
                return;

            // User left voice channel
            if (before.VoiceChannel != null &&
                (after.VoiceChannel == null || after.VoiceChannel.Id != before.VoiceChannel.Id))
            {
                var channelId = before.VoiceChannel.Id;
                var key = GetSessionKey(userId, channelId);

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

            // Quick check for server exclusion from local cache first
            if (IsGuildExcluded(guildId))
                return;

            // Get guild settings from local cache first
            var settings = await GetGuildSettingsAsync(guildId);

            // Skip if voice XP is disabled
            if (settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
                return;

            // Get all exclusions for this guild in a single query to populate local cache
            await GetExclusionsForGuildAsync(guildId);

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
                if (IsChannelExcluded(guildId, channel.Id))
                    continue;

                // Start sessions for eligible users
                foreach (var user in eligibleUsers.Where(user => !IsUserExcluded(guildId, user.Id)))
                {
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
            if (IsUserExcluded(guildId, userId))
                return;

            // Check if channel is excluded
            if (IsChannelExcluded(guildId, channelId))
                return;

            // Get or create session
            var key = GetSessionKey(userId, channelId);
            var session = voiceSessions.GetOrAdd(key, _ => new VoiceSession
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                SessionKey = key,
                JoinTime = timestamp,
                CurrentlyEligible = false,
                LastEligibilityChange = timestamp
            });

            // Check channel eligibility (requires 2+ users)
            var isEligible = IsChannelEligible(guildId, channelId);

            // If not in cache, check actual channel
            if (!isEligible)
            {
                isEligible = voiceChannel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u)) >= MinUsersForXp;
                channelEligibilityCache[(guildId, channelId)] = (isEligible, timestamp);
            }

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
        var key = GetSessionKey(userId, channelId);

        // Only proceed if we have an existing session
        if (!voiceSessions.TryGetValue(key, out var session))
            return;

        try
        {
            // Check overall channel eligibility
            var isChannelEligible = IsChannelEligible(guildId, channelId);

            // If not cached, check actual channel
            if (!isChannelEligible)
            {
                isChannelEligible = voiceChannel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u)) >= MinUsersForXp;
                channelEligibilityCache[(guildId, channelId)] = (isChannelEligible, timestamp);
            }

            // Is this user participating
            var isUserParticipating = IsParticipatingInVoice(user);

            // Update session eligibility based on current conditions
            var shouldBeEligible = isChannelEligible && isUserParticipating;

            if (shouldBeEligible && !session.CurrentlyEligible)
            {
                // Start new eligible period
                session.StartEligiblePeriod(timestamp);
            }
            else if (!shouldBeEligible && session.CurrentlyEligible)
            {
                // End current eligible period
                session.EndEligiblePeriod(timestamp);
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
        if (voiceSessions.IsEmpty)
            return;

        var startTime = DateTime.UtcNow;
        var sessionsUpdated = 0;

        try
        {
            // Group sessions by guild/channel for efficient processing
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
                    if (IsChannelExcluded(guildId, channelId))
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
                                    voiceSessions[session.SessionKey] = session;
                                    sessionsUpdated++;
                                    break;
                                case false when session.CurrentlyEligible:
                                    session.EndEligiblePeriod(startTime);
                                    voiceSessions[session.SessionKey] = session;
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
            }

            if (sessionsUpdated <= 0) return;
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            Log.Debug("Updated eligibility for {Count} voice sessions in {Time:F1}ms",
                sessionsUpdated, elapsedMs);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error validating voice sessions");
        }
    }

    /// <summary>
    ///     Cleans up stale data to prevent memory leaks.
    /// </summary>
    /// <param name="state">The state object.</param>
    private void CleanupStaleData(object state)
    {
        var now = DateTime.UtcNow;
        var sessionsRemoved = 0;

        try
        {
            var keysToRemove = new List<string>();

            foreach (var session in voiceSessions.Values)
            {
                try
                {
                    var guild = client.GetGuild(session.GuildId);
                    if (guild == null)
                    {
                        keysToRemove.Add(session.SessionKey);
                        continue;
                    }

                    var user = guild.GetUser(session.UserId);
                    if (user == null)
                    {
                        keysToRemove.Add(session.SessionKey);
                        continue;
                    }

                    // Check if user is still in this voice channel
                    if (user.VoiceChannel == null || user.VoiceChannel.Id != session.ChannelId)
                    {
                        keysToRemove.Add(session.SessionKey);
                    }
                }
                catch
                {
                    // If any error occurs, mark for removal to be safe
                    keysToRemove.Add(session.SessionKey);
                }
            }

            // Remove sessions
            sessionsRemoved += keysToRemove.Count(key => voiceSessions.TryRemove(key, out _));

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

            // Clear expired exclusion cache entries
            var expiredChannels = channelExclusionCache
                .Where(kvp => (now - kvp.Value.LastChecked) > exclusionCacheTtl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredChannels)
            {
                channelExclusionCache.TryRemove(key, out _);
            }

            var expiredUsers = userExclusionCache
                .Where(kvp => (now - kvp.Value.LastChecked) > exclusionCacheTtl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredUsers)
            {
                userExclusionCache.TryRemove(key, out _);
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

            // Log stats if anything significant happened
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
    ///     Gets exclusions for a guild and populates local caches.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task GetExclusionsForGuildAsync(ulong guildId)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Get exclusions from database
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get all exclusions for this guild in one query
            var exclusions = await db.XpExcludedItems
                .Where(x => x.GuildId == guildId)
                .ToListAsync();

            // Populate channel exclusion cache
            var channelExclusions = exclusions
                .Where(x => (ExcludedItemType)x.ItemType == ExcludedItemType.Channel)
                .Select(x => x.ItemId)
                .ToList();

            foreach (var channelId in channelExclusions)
            {
                channelExclusionCache[(guildId, channelId)] = (true, now);
            }

            // Populate user exclusion cache
            var userExclusions = exclusions
                .Where(x => (ExcludedItemType)x.ItemType == ExcludedItemType.User)
                .Select(x => x.ItemId)
                .ToList();

            foreach (var userId in userExclusions)
            {
                userExclusionCache[(guildId, userId)] = (true, now);
            }

            // Store role exclusions for reference in user checks
            var roleExclusions = exclusions
                .Where(x => (ExcludedItemType)x.ItemType == ExcludedItemType.Role)
                .Select(x => x.ItemId)
                .ToList();

            // We don't cache role exclusions directly, but use them to check users
            if (roleExclusions.Count > 0)
            {
                var guild = client.GetGuild(guildId);
                if (guild != null)
                {
                    // Find all users with excluded roles
                    foreach (var user in guild.Users)
                    {
                        if (user.Roles.Any(r => roleExclusions.Contains(r.Id)))
                        {
                            userExclusionCache[(guildId, user.Id)] = (true, now);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting exclusions for guild {GuildId}", guildId);
        }
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
    ///     Checks if a channel is excluded using local cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True if the channel is excluded.</returns>
    private bool IsChannelExcluded(ulong guildId, ulong channelId)
    {
        var now = DateTime.UtcNow;

        // Check local cache
        if (channelExclusionCache.TryGetValue((guildId, channelId), out var cacheEntry) &&
            (now - cacheEntry.LastChecked) < exclusionCacheTtl)
        {
            return cacheEntry.IsExcluded;
        }

        // Not in cache, we'll check database on next cycle
        return false;
    }

    /// <summary>
    ///     Checks if a user is excluded using local cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>True if the user is excluded.</returns>
    private bool IsUserExcluded(ulong guildId, ulong userId)
    {
        var now = DateTime.UtcNow;

        // Check local cache
        if (userExclusionCache.TryGetValue((guildId, userId), out var cacheEntry) &&
            (now - cacheEntry.LastChecked) < exclusionCacheTtl)
        {
            return cacheEntry.IsExcluded;
        }

        // Not in cache, we'll check database on next cycle
        return false;
    }

    /// <summary>
    ///     Checks if a channel is eligible for XP (has at least 2 users).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True if the channel is eligible.</returns>
    private bool IsChannelEligible(ulong guildId, ulong channelId)
    {
        var now = DateTime.UtcNow;

        // Check local cache
        if (channelEligibilityCache.TryGetValue((guildId, channelId), out var cacheEntry) &&
            (now - cacheEntry.LastChecked) < eligibilityCacheTtl)
        {
            return cacheEntry.IsEligible;
        }

        // Not in cache, we'll need to check - default to not eligible
        return false;
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
    ///     Gets the key for a voice session.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The session key.</returns>
    private static string GetSessionKey(ulong userId, ulong channelId)
    {
        return $"{userId}:{channelId}";
    }

    /// <summary>
    ///     Disposes resources used by the voice tracker.
    /// </summary>
    public void Dispose()
    {
        validationTimer?.Dispose();
        cleanupTimer?.Dispose();
        voiceSessions.Clear();
    }
}