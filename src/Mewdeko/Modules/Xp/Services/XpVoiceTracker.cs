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

    // Active voice sessions with capacity limit
    private readonly ConcurrentDictionary<string, VoiceXpSession> voiceSessions = new(
        Environment.ProcessorCount * 2, 1000); // Limit initial capacity and concurrency level

    // Timer for processing voice XP with adaptive interval
    private readonly Timer voiceXpTimer;
    private TimeSpan voiceXpInterval = TimeSpan.FromSeconds(60); // Start with 1 minute, adjust based on load
    private readonly SemaphoreSlim voiceProcessingSemaphore = new(1, 1); // Ensure only one processing task at a time

    // Constants to prevent abuse
    private const int MAX_ACTIVE_SESSIONS = 5000; // Limit total active sessions
    private const int MIN_USERS_FOR_XP = 2; // Need at least 2 users for XP

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

        // Initialize voice XP timer with adaptive interval
        voiceXpTimer = new Timer(ProcessVoiceXp, null, TimeSpan.FromSeconds(30), voiceXpInterval);

        // Register cleanup task to periodically remove stale sessions
        var cleanupTimer = new Timer(CleanupStaleSessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
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
            // Quick check for server exclusion (cached)
            if (await cacheManager.IsServerExcludedAsync(guildUser.Guild.Id))
                return;

            // User left voice channel
            if (before.VoiceChannel != null &&
                (after.VoiceChannel == null || after.VoiceChannel.Id != before.VoiceChannel.Id))
            {
                await EndVoiceSession(guildUser, before.VoiceChannel);
            }

            // User joined voice channel
            if (after.VoiceChannel != null &&
                (before.VoiceChannel == null || before.VoiceChannel.Id != after.VoiceChannel.Id))
            {
                // Only start session if we're below the max limit
                if (voiceSessions.Count < MAX_ACTIVE_SESSIONS)
                {
                    await StartVoiceSession(guildUser, after.VoiceChannel);
                }
                else
                {
                    Log.Warning(
                        "Voice session limit reached ({Count}/{Max}). Cannot start session for {UserId} in {GuildId}",
                        voiceSessions.Count, MAX_ACTIVE_SESSIONS, guildUser.Id, guildUser.Guild.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling voice state update for {UserId} in {GuildId}", user.Id, guildUser.Guild.Id);
        }
    }

    /// <summary>
    ///     Scans a guild's voice channels and sets up voice sessions.
    /// </summary>
    /// <param name="guild">The guild to scan.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ScanGuildVoiceChannels(SocketGuild guild)
    {
        // Early exit if we're already at the session limit
        if (voiceSessions.Count >= MAX_ACTIVE_SESSIONS)
        {
            Log.Warning("Cannot scan guild voice channels: session limit reached ({Count}/{Max})",
                voiceSessions.Count, MAX_ACTIVE_SESSIONS);
            return;
        }

        // Batch process voice channels
        var validChannels = guild.VoiceChannels
            .Where(ShouldTrackVoiceChannel)
            .ToList();

        foreach (var channel in validChannels)
        {
            try
            {
                await ScanVoiceChannel(channel);

                // Break if we reach the limit during scanning
                if (voiceSessions.Count >= MAX_ACTIVE_SESSIONS)
                    break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error scanning voice channel {ChannelId} in guild {GuildId}",
                    channel.Id, guild.Id);
            }
        }
    }

    /// <summary>
    ///     Starts a voice XP session for a user.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="voiceChannel">The voice channel.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task StartVoiceSession(SocketGuildUser user, SocketVoiceChannel voiceChannel)
    {
        // Skip if excluded or if voice XP is disabled or if we've reached the limit
        if (!ShouldTrackVoiceChannel(voiceChannel) ||
            !await CanGainVoiceXp(user.Id, user.Guild.Id, voiceChannel.Id) ||
            voiceSessions.Count >= MAX_ACTIVE_SESSIONS)
            return;

        var key = GetVoiceSessionKey(user.Id, voiceChannel.Id);
        var now = DateTime.UtcNow;

        // Use TryAdd to prevent duplicate sessions
        voiceSessions.TryAdd(key, new VoiceXpSession
        {
            UserId = user.Id,
            GuildId = user.Guild.Id,
            ChannelId = voiceChannel.Id,
            StartTime = now,
            LastProcessed = now,
            SessionKey = key
        });
    }

    /// <summary>
    ///     Ends a voice XP session for a user.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="voiceChannel">The voice channel.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task EndVoiceSession(SocketGuildUser user, SocketVoiceChannel voiceChannel)
    {
        var key = GetVoiceSessionKey(user.Id, voiceChannel.Id);

        if (voiceSessions.TryRemove(key, out var session))
        {
            try
            {
                var settings = await cacheManager.GetGuildXpSettingsAsync(user.Guild.Id);

                // Skip if voice XP is disabled
                if (settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
                    return;

                // Calculate XP for final minutes
                var now = DateTime.UtcNow;
                var elapsedMinutes = (now - session.LastProcessed).TotalMinutes;

                if (elapsedMinutes < 0.25) // Less than 15 seconds
                    return;

                // Cap elapsed minutes to prevent abuse
                elapsedMinutes = Math.Min(elapsedMinutes, settings.VoiceXpTimeout);

                // Calculate final XP
                var baseXp = Math.Min(settings.VoiceXpPerMinute, XpService.MaxVoiceXpPerMinute);
                var finalXp = (int)(baseXp * elapsedMinutes);

                // Apply multipliers
                finalXp = (int)(finalXp *
                                await cacheManager.GetEffectiveMultiplierAsync(user.Id, user.Guild.Id,
                                    voiceChannel.Id));

                if (finalXp <= 0)
                    return;

                // Add to background processor queue
                XpService.Instance?.QueueXpGain(
                    user.Guild.Id,
                    user.Id,
                    finalXp,
                    voiceChannel.Id,
                    XpSource.Voice
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error ending voice session for {UserId} in guild {GuildId}",
                    user.Id, user.Guild.Id);
            }
        }
    }

    /// <summary>
    ///     Processes voice XP for active sessions with batch processing.
    /// </summary>
    private async void ProcessVoiceXp(object state)
    {
        // Exit if no sessions or already processing
        if (voiceSessions.IsEmpty || !voiceProcessingSemaphore.Wait(0))
            return;

        try
        {
            var startTime = DateTime.UtcNow;
            var now = startTime;
            var sessionsProcessed = 0;
            var backgroundProcessor = XpService.Instance?.GetBackgroundProcessor();
            if (backgroundProcessor == null)
                return;

            // Group sessions by guild for batch processing of settings and exclusions
            var sessionsByGuild = voiceSessions.Values
                .Where(s => s.LastProcessed.AddSeconds(60) <= now) // Only process sessions due for update
                .GroupBy(s => s.GuildId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Prepare batch operations
            var sessionsToRemove = new List<string>();
            var xpToAward = new List<(ulong GuildId, ulong UserId, int XpAmount, ulong ChannelId)>();

            // Cache guild settings for this batch to minimize DB calls
            var guildSettings = new ConcurrentDictionary<ulong, GuildXpSetting>();
            var excludedServers = new ConcurrentDictionary<ulong, bool>();

            foreach (var guildEntry in sessionsByGuild)
            {
                var guildId = guildEntry.Key;
                var guildSessions = guildEntry.Value;

                // Skip if server is excluded (check once per guild instead of per user)
                if (await cacheManager.IsServerExcludedAsync(guildId))
                {
                    excludedServers[guildId] = true;
                    guildSessions.ForEach(s => sessionsToRemove.Add(s.SessionKey));
                    continue;
                }

                excludedServers[guildId] = false;

                // Get settings once per guild
                var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);
                guildSettings[guildId] = settings;

                // Skip this guild if voice XP is disabled
                if (settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
                {
                    guildSessions.ForEach(s => sessionsToRemove.Add(s.SessionKey));
                    continue;
                }

                // Get guild once per guild rather than per user
                var guild = client.GetGuild(guildId);
                if (guild == null)
                {
                    guildSessions.ForEach(s => sessionsToRemove.Add(s.SessionKey));
                }
            }

            // Batch fetch exclusion data
            await using var db = await dbFactory.CreateConnectionAsync();
            var allGuildIds = sessionsByGuild.Keys.ToList();

            // Get all channel exclusions for these guilds in one query
            var excludedChannels = await db.XpExcludedItems
                .Where(x => allGuildIds.Contains(x.GuildId) &&
                            (ExcludedItemType)x.ItemType == ExcludedItemType.Channel)
                .Select(x => new
                {
                    x.GuildId, x.ItemId
                })
                .ToDictionaryAsync(
                    k => (k.GuildId, k.ItemId),
                    v => true);

            // Process each session
            foreach (var guildEntry in sessionsByGuild)
            {
                var guildId = guildEntry.Key;
                var guildSessions = guildEntry.Value;

                // Skip if server is excluded
                if (excludedServers[guildId])
                    continue;

                // Get cached settings
                var settings = guildSettings[guildId];
                var guild = client.GetGuild(guildId);

                foreach (var session in guildSessions)
                {
                    try
                    {
                        sessionsProcessed++;

                        // Check if channel is excluded using cached exclusions
                        if (excludedChannels.TryGetValue((guildId, session.ChannelId), out _))
                        {
                            sessionsToRemove.Add(session.SessionKey);
                            continue;
                        }

                        // Verify user is still active in voice
                        var user = guild?.GetUser(session.UserId);
                        var channel = guild?.GetVoiceChannel(session.ChannelId);

                        if (user == null || channel == null || user.VoiceChannel?.Id != channel.Id)
                        {
                            // User is no longer in this voice channel, mark for removal
                            sessionsToRemove.Add(session.SessionKey);
                            continue;
                        }

                        // Check if channel still meets requirements
                        if (!ShouldTrackVoiceChannel(channel))
                        {
                            sessionsToRemove.Add(session.SessionKey);
                            continue;
                        }

                        // Calculate minutes since last process
                        var minutesSinceLastProcess = (now - session.LastProcessed).TotalMinutes;

                        // Maximum elapsed minutes is the timeout setting
                        minutesSinceLastProcess = Math.Min(minutesSinceLastProcess, settings.VoiceXpTimeout);

                        // Calculate XP to award
                        var baseXp = Math.Min(settings.VoiceXpPerMinute, XpService.MaxVoiceXpPerMinute);
                        var xpToAwardAmount = (int)(baseXp * minutesSinceLastProcess);

                        // Apply multipliers
                        xpToAwardAmount = (int)(xpToAwardAmount * await cacheManager.GetEffectiveMultiplierAsync(
                            session.UserId, session.GuildId, session.ChannelId));

                        if (xpToAwardAmount <= 0)
                            continue;

                        // Add XP to batch queue
                        xpToAward.Add((guildId, session.UserId, xpToAwardAmount, session.ChannelId));

                        // Update last processed time
                        session.LastProcessed = now;

                        // Update entry in dictionary
                        voiceSessions[session.SessionKey] = session;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing voice XP for user {UserId} in guild {GuildId}",
                            session.UserId, session.GuildId);
                    }
                }
            }

            // Remove sessions marked for removal (in batches to reduce contention)
            foreach (var key in sessionsToRemove)
            {
                voiceSessions.TryRemove(key, out _);
            }

            // Process all XP awards in batches
            foreach (var (guildId, userId, xpAmount, channelId) in xpToAward)
            {
                backgroundProcessor.QueueXpGain(
                    guildId,
                    userId,
                    xpAmount,
                    channelId,
                    XpSource.Voice
                );
            }

            // Adjust the timer interval based on load
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            AdjustTimerInterval(processingTime, sessionsProcessed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in voice XP processing");
        }
        finally
        {
            voiceProcessingSemaphore.Release();
        }
    }

    /// <summary>
    ///     Dynamically adjusts the timer interval based on processing load.
    /// </summary>
    private void AdjustTimerInterval(double processingTimeMs, int sessionsProcessed)
    {
        // Don't adjust if we processed very few sessions
        if (sessionsProcessed < 10)
            return;

        // Calculate optimal interval
        var targetIntervalSeconds = 60.0; // Default 1 minute

        if (processingTimeMs > 500)
        {
            // If processing takes more than 500ms, increase interval
            targetIntervalSeconds = Math.Min(120, 60.0 * (processingTimeMs / 300.0));
        }
        else if (processingTimeMs < 100 && voiceSessions.Count > 100)
        {
            // If processing is very fast and we have many sessions, decrease interval
            targetIntervalSeconds = Math.Max(30, 60.0 * (processingTimeMs / 300.0));
        }

        // Update interval if it changed significantly (more than 10 seconds)
        if (Math.Abs(targetIntervalSeconds - voiceXpInterval.TotalSeconds) > 10)
        {
            voiceXpInterval = TimeSpan.FromSeconds(targetIntervalSeconds);
            voiceXpTimer.Change(voiceXpInterval, voiceXpInterval);

            Log.Debug("Adjusted voice XP timer interval to {Interval} seconds. " +
                      "Processing time: {ProcessingTime}ms for {Sessions} sessions",
                voiceXpInterval.TotalSeconds, processingTimeMs, sessionsProcessed);
        }
    }

    /// <summary>
    ///     Removes stale voice sessions to prevent memory leaks.
    /// </summary>
    private void CleanupStaleSessions(object state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var staleTime = now.AddHours(-3); // Sessions inactive for 3 hours
            var keysToRemove = new List<string>();

            // Find all stale sessions
            foreach (var session in voiceSessions.Values)
            {
                if (session.LastProcessed < staleTime)
                {
                    keysToRemove.Add(session.SessionKey);
                }
            }

            // Remove stale sessions
            foreach (var key in keysToRemove)
            {
                voiceSessions.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                Log.Debug("Cleaned up {Count} stale voice sessions", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning up stale voice sessions");
        }
    }

    /// <summary>
    ///     Scans a voice channel and starts sessions for eligible users.
    /// </summary>
    /// <param name="voiceChannel">The voice channel to scan.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ScanVoiceChannel(SocketVoiceChannel voiceChannel)
    {
        if (!ShouldTrackVoiceChannel(voiceChannel))
            return;

        // Check if we have space for potential new sessions
        var potentialNewSessions = voiceChannel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u));
        if (voiceSessions.Count + potentialNewSessions > MAX_ACTIVE_SESSIONS)
            return;

        // Check for channel exclusion once instead of per user
        if (!await CanChannelGainXp(voiceChannel.Guild.Id, voiceChannel.Id))
            return;

        // Batch process eligible users
        foreach (var user in voiceChannel.Users)
        {
            if (user.IsBot || !IsParticipatingInVoice(user))
                continue;

            if (await CanUserGainVoiceXp(user.Id, user.Guild.Id))
            {
                await StartVoiceSession(user, voiceChannel);
            }
        }
    }

    /// <summary>
    ///     Determines if a voice channel should be tracked for XP.
    /// </summary>
    /// <param name="channel">The voice channel.</param>
    /// <returns>True if the channel should be tracked, false otherwise.</returns>
    private bool ShouldTrackVoiceChannel(SocketVoiceChannel channel)
    {
        // Need at least MIN_USERS_FOR_XP non-bot, participating users
        return channel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u)) >= MIN_USERS_FOR_XP;
    }

    /// <summary>
    ///     Determines if a user is actively participating in a voice channel.
    /// </summary>
    /// <param name="user">The voice state user.</param>
    /// <returns>True if the user is participating, false otherwise.</returns>
    private bool IsParticipatingInVoice(IVoiceState user)
    {
        return !user.IsDeafened && !user.IsMuted && !user.IsSelfDeafened && !user.IsSelfMuted;
    }

    /// <summary>
    ///     Gets the key for a voice session.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The session key.</returns>
    private string GetVoiceSessionKey(ulong userId, ulong channelId)
    {
        return $"{userId}:{channelId}";
    }

    /// <summary>
    ///     Checks if a channel can gain voice XP (not excluded).
    /// </summary>
    private async Task<bool> CanChannelGainXp(ulong guildId, ulong channelId)
    {
        // Check server exclusion
        if (await cacheManager.IsServerExcludedAsync(guildId))
            return false;

        // Check if voice XP is enabled
        var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);
        if (settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
            return false;

        // Check channel exclusion
        await using var db = await dbFactory.CreateConnectionAsync();

        return !await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId &&
                           x.ItemId == channelId &&
                           (ExcludedItemType)x.ItemType == ExcludedItemType.Channel);
    }

    /// <summary>
    ///     Checks if a user can gain voice XP (not excluded and roles not excluded).
    /// </summary>
    private async Task<bool> CanUserGainVoiceXp(ulong userId, ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check user exclusion
        var userExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId &&
                           x.ItemId == userId &&
                           (ExcludedItemType)x.ItemType == ExcludedItemType.User);

        if (userExcluded)
            return false;

        // Check role exclusions
        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);

        if (user != null)
        {
            var excludedRoles = await db.XpExcludedItems
                .Where(x => x.GuildId == guildId &&
                            (ExcludedItemType)x.ItemType == ExcludedItemType.Role)
                .Select(x => x.ItemId)
                .ToListAsync();

            if (user.Roles.Select(x => x.Id).Any(r => excludedRoles.Contains(r)))
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Overall check if a user can gain voice XP in a channel.
    /// </summary>
    private async Task<bool> CanGainVoiceXp(ulong userId, ulong guildId, ulong channelId)
    {
        // Check server and channel status
        if (!await CanChannelGainXp(guildId, channelId))
            return false;

        // Check user and role status
        return await CanUserGainVoiceXp(userId, guildId);
    }

    /// <summary>
    ///     Disposes of resources used by the voice tracker.
    /// </summary>
    public void Dispose()
    {
        voiceXpTimer?.Dispose();
        voiceProcessingSemaphore?.Dispose();
        voiceSessions.Clear();
    }
}