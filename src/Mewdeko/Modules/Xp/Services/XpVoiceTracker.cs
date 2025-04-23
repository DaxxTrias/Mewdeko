using System.Threading;
using LinqToDB;
using Mewdeko.Database.EF.EFCore.Enums;
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

    // Active voice sessions
    private readonly ConcurrentDictionary<string, VoiceXpSession> voiceSessions = new();

    // Timer for processing voice XP
    private readonly Timer voiceXpTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpVoiceTracker"/> class.
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

        // Initialize voice XP timer
        voiceXpTimer = new Timer(ProcessVoiceXp, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
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
            if (before.VoiceChannel != null && (after.VoiceChannel == null || after.VoiceChannel.Id != before.VoiceChannel.Id))
            {
                await EndVoiceSession(guildUser, before.VoiceChannel);
            }

            // User joined voice channel
            if (after.VoiceChannel != null && (before.VoiceChannel == null || before.VoiceChannel.Id != after.VoiceChannel.Id))
            {
                await StartVoiceSession(guildUser, after.VoiceChannel);
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
        foreach (var channel in guild.VoiceChannels)
        {
            try
            {
                await ScanVoiceChannel(channel);
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
        // Skip if excluded or if voice XP is disabled
        if (!ShouldTrackVoiceChannel(voiceChannel) || !await CanGainVoiceXp(user.Id, user.Guild.Id, voiceChannel.Id))
            return;

        var key = GetVoiceSessionKey(user.Id, voiceChannel.Id);
        var now = DateTime.UtcNow;

        voiceSessions[key] = new VoiceXpSession
        {
            UserId = user.Id,
            GuildId = user.Guild.Id,
            ChannelId = voiceChannel.Id,
            StartTime = now,
            LastProcessed = now,
            SessionKey = key
        };
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
                finalXp = (int)(finalXp * await cacheManager.GetEffectiveMultiplierAsync(user.Id, user.Guild.Id, voiceChannel.Id));

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
    ///     Processes voice XP for active sessions.
    /// </summary>
    private async void ProcessVoiceXp(object state)
    {
        if (voiceSessions.IsEmpty)
            return;

        try
        {
            var now = DateTime.UtcNow;
            var backgroundProcessor = XpService.Instance?.GetBackgroundProcessor();
            if (backgroundProcessor == null)
                return;

            // Process each session
            foreach (var session in voiceSessions.Values.ToList())
            {
                try
                {
                    // Skip if not due for processing (1 minute interval)
                    if (session.LastProcessed.AddSeconds(60) > now)
                        continue;

                    // Skip sessions in excluded guilds
                    if (await cacheManager.IsServerExcludedAsync(session.GuildId))
                        continue;

                    // Get settings and check if voice XP is disabled
                    var settings = await cacheManager.GetGuildXpSettingsAsync(session.GuildId);
                    if (settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
                        continue;

                    // Verify user is still active in voice
                    var guild = client.GetGuild(session.GuildId);
                    var user = guild?.GetUser(session.UserId);
                    var channel = guild?.GetVoiceChannel(session.ChannelId);

                    if (user == null || channel == null || user.VoiceChannel?.Id != channel.Id)
                    {
                        // User is no longer in this voice channel, remove session
                        voiceSessions.TryRemove(session.SessionKey, out _);
                        continue;
                    }

                    // Check if channel still meets requirements
                    if (!ShouldTrackVoiceChannel(channel) || !await CanGainVoiceXp(session.UserId, session.GuildId, session.ChannelId))
                        continue;

                    // Calculate minutes since last process
                    var minutesSinceLastProcess = (now - session.LastProcessed).TotalMinutes;

                    // Maximum elapsed minutes is the timeout setting (prevent abuse if timer missed cycles)
                    minutesSinceLastProcess = Math.Min(minutesSinceLastProcess, settings.VoiceXpTimeout);

                    // Calculate XP to award
                    var baseXp = Math.Min(settings.VoiceXpPerMinute, XpService.MaxVoiceXpPerMinute);
                    var xpToAward = (int)(baseXp * minutesSinceLastProcess);

                    // Apply multipliers
                    xpToAward = (int)(xpToAward * await cacheManager.GetEffectiveMultiplierAsync(session.UserId, session.GuildId, session.ChannelId));

                    if (xpToAward <= 0)
                        continue;

                    // Add XP to queue
                    backgroundProcessor.QueueXpGain(
                        session.GuildId,
                        session.UserId,
                        xpToAward,
                        session.ChannelId,
                        XpSource.Voice
                    );

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
        catch (Exception ex)
        {
            Log.Error(ex, "Error in voice XP processing");
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

        foreach (var user in voiceChannel.Users)
        {
            if (user.IsBot || !IsParticipatingInVoice(user))
                continue;

            if (await CanGainVoiceXp(user.Id, user.Guild.Id, voiceChannel.Id))
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
        // Need at least 2 non-bot, participating users
        return channel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u)) >= 2;
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
    ///     Checks if a user can gain voice XP.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True if the user can gain voice XP, false otherwise.</returns>
    private async Task<bool> CanGainVoiceXp(ulong userId, ulong guildId, ulong channelId)
    {
        // Check server exclusion
        if (await cacheManager.IsServerExcludedAsync(guildId))
            return false;

        // Check if voice XP is enabled
        var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);
        if (settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
            return false;

        // Check user and channel exclusions
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Check channel exclusion
        var channelExcluded = await dbContext.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId && x.ItemId == channelId && (ExcludedItemType)x.ItemType == ExcludedItemType.Channel);

        if (channelExcluded)
            return false;

        // Check user exclusion
        var userExcluded = await dbContext.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId && x.ItemId == userId && (ExcludedItemType)x.ItemType == ExcludedItemType.User);

        if (userExcluded)
            return false;

        // Check role exclusions
        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);

        if (user != null)
        {
            var excludedRoles = await dbContext.XpExcludedItems
                .Where(x => x.GuildId == guildId && (ExcludedItemType)x.ItemType == ExcludedItemType.Role)
                .Select(x => x.ItemId)
                .ToListAsync();

            if (user.Roles.Select(x => x.Id).Any(r => excludedRoles.Contains(r)))
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Disposes of resources used by the voice tracker.
    /// </summary>
    public void Dispose()
    {
        voiceXpTimer?.Dispose();
        voiceSessions.Clear();
    }
}