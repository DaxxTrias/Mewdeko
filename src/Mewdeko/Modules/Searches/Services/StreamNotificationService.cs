#nullable enable
using System.Net.Http;
using System.Threading;
using LinqToDB;
using DataModel;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Common;
using Mewdeko.Modules.Searches.Common.StreamNotifications;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Mewdeko.Modules.Searches.Services.Common;
using Mewdeko.Services.strings;
using Serilog;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
///     Service responsible for managing and tracking online stream notifications.
/// </summary>
public class StreamNotificationService : IReadyExecutor, INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly Random rng = new MewdekoRandom();
    private readonly object shardLock = new();
    private readonly NotifChecker streamTracker;
    private readonly IBotStrings strings;

    private Dictionary<StreamDataKey, Dictionary<ulong, HashSet<FollowedStream>>> shardTrackedStreams = new ();
    private Dictionary<StreamDataKey, HashSet<ulong>> trackCounter = new();
    private List<ulong> OfflineNotificationServers { get; set; } = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamNotificationService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database service instance.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="strings">The bot string service instance.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="httpFactory">The HTTP client factory.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="eventHandler">The event handler for guild events.</param>
    public StreamNotificationService(
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        IBotStrings strings,
        IBotCredentials creds,
        IHttpClientFactory httpFactory,
        Mewdeko bot,
        EventHandler eventHandler)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.strings = strings;
        streamTracker = new NotifChecker(httpFactory, creds, creds.RedisKey(), true);

        // Load all followed streams from database
        _ = Task.Run(async () =>
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Load offline notification servers
            OfflineNotificationServers = await db.GuildConfigs
                .Where(x => x.NotifyStreamOffline)
                .Select(x => x.GuildId)
                .ToListAsync();

            // Load followed streams
            var followedStreams = await db.FollowedStreams.ToListAsync();

            // Group streams by type and name
            shardTrackedStreams = followedStreams.GroupBy(x => new
                {
                    x.Type, Name = x.Username?.ToLower()
                })
                .ToList()
                .ToDictionary(
                    x => new StreamDataKey((FType)x.Key.Type, x.Key.Name?.ToLower()),
                    x => x.GroupBy(y => y.GuildId)
                        .ToDictionary(y => y.Key,
                            y => y.AsEnumerable().ToHashSet()));

            // Cache all streams in the tracker
            foreach (var fs in followedStreams)
                await streamTracker.CacheAddData(fs.CreateKey(), null, false);

            // Create counter dictionary for tracking
            trackCounter = followedStreams.GroupBy(x => new
                {
                    x.Type, Name = x.Username?.ToLower()
                })
                .ToDictionary(x => new StreamDataKey((FType)x.Key.Type, x.Key.Name),
                    x => x.Select(fs => fs.GuildId).ToHashSet());
        });

        // Set up stream notification event handlers
        streamTracker.OnStreamsOffline += HandleStreamsOffline;
        streamTracker.OnStreamsOnline += HandleStreamsOnline;

        // Start the stream tracker
        _ = streamTracker.RunAsync();

        // Register guild events
        bot.JoinedGuild += ClientOnJoinedGuild;
        eventHandler.LeftGuild += ClientOnLeftGuild;
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                try
                {
                    var errorLimit = TimeSpan.FromHours(12);
                    var failingStreams = streamTracker.GetFailingStreams(errorLimit, true).ToList();

                    if (failingStreams.Count == 0)
                        continue;

                    var deleteGroups = failingStreams.GroupBy(x => x.Type)
                        .ToDictionary(x => x.Key, x => x.Select(y => y.Name).ToList());

                    foreach (var kvp in deleteGroups)
                    {
                        Log.Information(
                            "Deleting {StreamCount} {Platform} streams because they've been erroring for more than {ErrorLimit}: {RemovedList}",
                            kvp.Value.Count,
                            kvp.Key,
                            errorLimit,
                            string.Join(", ", kvp.Value));

                        await using var db = await dbFactory.CreateConnectionAsync();

                        // Delete streams using batch deletion with LinqToDB
                        await db.FollowedStreams
                            .Where(x => (FType)x.Type == kvp.Key && kvp.Value.Contains(x.Username))
                            .DeleteAsync();

                        // Untrack each deleted stream
                        foreach (var loginToDelete in kvp.Value)
                            await streamTracker.UntrackStreamByKey(new StreamDataKey(kvp.Key, loginToDelete));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error cleaning up FollowedStreams");
                }
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles streams coming online and sends notifications.
    /// </summary>
    /// <param name="onlineStreams">The list of streams that came online.</param>
    private async Task HandleStreamsOnline(List<StreamData> onlineStreams)
    {
        foreach (var stream in onlineStreams)
        {
            var key = stream.CreateKey();
            if (shardTrackedStreams.TryGetValue(key, out var fss))
            {
                await fss.SelectMany(x => x.Value)
                    .Select(fs =>
                    {
                        var textChannel = client.GetGuild(fs.GuildId)?.GetTextChannel(fs.ChannelId);

                        if (textChannel is null)
                            return Task.CompletedTask;

                        var rep = new ReplacementBuilder().WithOverride("%user%", () => fs.Username)
                            .WithOverride("%platform%", () => fs.Type.ToString())
                            .Build();

                        var message = string.IsNullOrWhiteSpace(fs.Message) ? "" : rep.Replace(fs.Message);

                        return textChannel.EmbedAsync(GetEmbed(fs.GuildId, stream), message);
                    })
                    .WhenAll().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Handles streams going offline and sends notifications.
    /// </summary>
    /// <param name="offlineStreams">The list of streams that went offline.</param>
    private async Task HandleStreamsOffline(List<StreamData> offlineStreams)
    {
        foreach (var stream in offlineStreams)
        {
            var key = stream.CreateKey();
            if (shardTrackedStreams.TryGetValue(key, out var fss))
            {
                // Only send offline notifications to guilds that have them enabled
                await fss
                    .SelectMany(x => x.Value)
                    .Where(x => OfflineNotificationServers.Contains(x.GuildId))
                    .Select(fs => client.GetGuild(fs.GuildId)
                        ?.GetTextChannel(fs.ChannelId)
                        ?.EmbedAsync(GetEmbed(fs.GuildId, stream)))
                    .Where(task => task != null)
                    .Select(task => task!)
                    .WhenAll()
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task ClientOnJoinedGuild(GuildConfig guildConfig)
    {
        var guildId = guildConfig.GuildId;
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config for notification setting
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (gc is null)
            return;

        if (gc.NotifyStreamOffline)
            OfflineNotificationServers.Add(guildId);

        // Get followed streams for this guild
        var followedStreams = await db.FollowedStreams
            .Where(fs => fs.GuildId == guildId)
            .ToListAsync();

        foreach (var followedStream in followedStreams)
        {
            var key = followedStream.CreateKey();
            var streams = GetLocalGuildStreams(key, guildId);
            streams.Add(followedStream);
            TrackStream(followedStream);
        }
    }

    private async Task ClientOnLeftGuild(SocketGuild guild)
    {
        var guildId = guild.Id;
        await using var db = await dbFactory.CreateConnectionAsync();

        if (OfflineNotificationServers.Contains(guildId))
            OfflineNotificationServers.Remove(guildId);

        // Get followed streams for this guild
        var followedStreams = await db.FollowedStreams
            .Where(fs => fs.GuildId == guildId)
            .ToListAsync();

        foreach (var followedStream in followedStreams)
        {
            var streams = GetLocalGuildStreams(followedStream.CreateKey(), guildId);
            streams.Remove(followedStream);
            await UntrackStream(followedStream);
        }
    }

    /// <summary>
    ///     Clears all followed streams for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The number of streams removed.</returns>
    public async Task<int> ClearAllStreams(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get followed streams for this guild
        var followedStreams = await db.FollowedStreams
            .Where(fs => fs.GuildId == guildId)
            .ToListAsync();

        var removedCount = followedStreams.Count;

        if (removedCount > 0)
        {
            // Remove streams using batch deletion
            await db.FollowedStreams
                .Where(fs => fs.GuildId == guildId)
                .DeleteAsync();

            // Untrack each removed stream
            foreach (var s in followedStreams)
                await UntrackStream(s).ConfigureAwait(false);
        }

        return removedCount;
    }

    /// <summary>
    ///     Unfollows a stream for a guild and removes it from the tracked streams.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the stream to unfollow.</param>
    /// <returns>The unfollowed stream data if successful, otherwise <see langword="null" />.</returns>
    public async Task<FollowedStream?> UnfollowStreamAsync(ulong guildId, int index)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get streams for this guild, ordered by ID
        var fss = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        // Out of range check
        if (fss.Count <= index)
            return null;

        var fs = fss[index];

        // Delete the stream
        await db.FollowedStreams
            .Where(x => x.Id == fs.Id)
            .DeleteAsync();

        // Remove from local cache
        lock (shardLock)
        {
            var key = fs.CreateKey();
            var streams = GetLocalGuildStreams(key, guildId);
            streams.Remove(fs);
        }

        await UntrackStream(fs).ConfigureAwait(false);

        return fs;
    }

    /// <summary>
    ///     Adds a stream to the tracking system.
    /// </summary>
    /// <param name="fs">The followed stream to track.</param>
    private void TrackStream(FollowedStream fs)
    {
        var key = fs.CreateKey();
        if (trackCounter.TryGetValue(key, out _))
        {
            trackCounter[key].Add(fs.GuildId);
        }
        else
        {
            trackCounter[key] = [fs.GuildId];
        }

        _ = streamTracker.CacheAddData(key, null, false);
    }

    /// <summary>
    ///     Removes a stream from the tracking system.
    /// </summary>
    /// <param name="fs">The followed stream to untrack.</param>
    private async Task UntrackStream(FollowedStream fs)
    {
        var key = fs.CreateKey();
        if (!trackCounter.TryGetValue(key, out var set))
        {
            // It should've been removed already?
            await streamTracker.UntrackStreamByKey(key);
            return;
        }

        set.Remove(fs.GuildId);
        if (set.Count != 0)
            return;

        trackCounter.Remove(key);
        // If no other guilds are following this stream, untrack it
        await streamTracker.UntrackStreamByKey(key);
    }

    /// <summary>
    ///     Follows a stream for a guild and adds it to the tracked streams.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel.</param>
    /// <param name="url">The URL of the stream.</param>
    /// <returns>The stream data if successful, otherwise <see langword="null" />.</returns>
    public async Task<StreamData?> FollowStream(ulong guildId, ulong channelId, string url)
    {
        var data = await streamTracker.GetStreamDataByUrlAsync(url).ConfigureAwait(false);

        if (data is null)
            return null;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Check stream count limit
        var streamCount = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .CountAsync();

        if (streamCount >= 10)
            return null;

        // Create new stream with guild data
        var fs = new FollowedStream
        {
            Type = (int)data.StreamType,
            Username = data.UniqueName,
            ChannelId = channelId,
            GuildId = guildId
        };

        // Insert the new stream
        await db.InsertAsync(fs);

        // Add to local cache
        lock (shardLock)
        {
            var key = data.CreateKey();
            var streams = GetLocalGuildStreams(key, guildId);
            streams.Add(fs);
        }

        TrackStream(fs);
        return data;
    }

    /// <summary>
    ///     Gets the embed for a stream status.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="status">The stream status data.</param>
    /// <returns>An embed builder with the stream status information.</returns>
    public EmbedBuilder GetEmbed(ulong guildId, StreamData status)
    {
        var embed = new EmbedBuilder()
            .WithTitle(status.Name)
            .WithUrl(status.StreamUrl)
            .WithDescription(status.StreamUrl)
            .AddField(efb => efb.WithName(GetText(guildId, "status"))
                .WithValue(status.IsLive ? "🟢 Online" : "🔴 Offline")
                .WithIsInline(true))
            .AddField(efb => efb.WithName(GetText(guildId, "viewers"))
                .WithValue(status.IsLive ? status.Viewers.ToString() : "-")
                .WithIsInline(true))
            .WithColor(status.IsLive ? Mewdeko.OkColor : Mewdeko.ErrorColor);

        if (!string.IsNullOrWhiteSpace(status.Title))
            embed.WithAuthor(status.Title);

        if (!string.IsNullOrWhiteSpace(status.Game))
            embed.AddField(GetText(guildId, "streaming"), status.Game, true);

        if (!string.IsNullOrWhiteSpace(status.AvatarUrl))
            embed.WithThumbnailUrl(status.AvatarUrl);

        if (!string.IsNullOrWhiteSpace(status.Preview))
            embed.WithImageUrl($"{status.Preview}?dv={rng.Next()}");

        return embed;
    }

    private string? GetText(ulong guildId, string key, params object[] replacements)
    {
        return strings.GetText(key, guildId, replacements);
    }

    /// <summary>
    ///     Toggles the notification for offline streams for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns><see langword="true" /> if notifications are enabled, <see langword="false" /> otherwise.</returns>
    public async Task<bool> ToggleStreamOffline(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (gc == null)
            return false;

        // Toggle setting
        gc.NotifyStreamOffline = !gc.NotifyStreamOffline;

        // Update database
        await db.UpdateAsync(gc);

        // Update local list
        if (gc.NotifyStreamOffline)
            OfflineNotificationServers.Add(guildId);
        else
            OfflineNotificationServers.Remove(guildId);

        return gc.NotifyStreamOffline;
    }

    /// <summary>
    ///     Retrieves stream data for a given URL.
    /// </summary>
    /// <param name="url">The URL of the stream.</param>
    /// <returns>The stream data if available, otherwise <see langword="null" />.</returns>
    public Task<StreamData?> GetStreamDataAsync(string url)
    {
        return streamTracker.GetStreamDataByUrlAsync(url);
    }

    private HashSet<FollowedStream> GetLocalGuildStreams(in StreamDataKey key, ulong guildId)
    {
        if (shardTrackedStreams.TryGetValue(key, out var map))
        {
            if (map.TryGetValue(guildId, out var set))
                return set;
            return map[guildId] = [];
        }

        shardTrackedStreams[key] = new Dictionary<ulong, HashSet<FollowedStream>>
        {
            {
                guildId, []
            }
        };
        return shardTrackedStreams[key][guildId];
    }

    /// <summary>
    ///     Sets a custom message for a followed stream.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the stream to set the message for.</param>
    /// <param name="message">The custom message to set.</param>
    /// <returns>A tuple containing whether the operation was successful and the updated stream.</returns>
    public async Task<(bool, FollowedStream)> SetStreamMessage(
        ulong guildId,
        int index,
        string message)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get streams for this guild, ordered by ID
        var fss = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (fss.Count <= index)
        {
            return (false, null)!;
        }

        var fs = fss[index];
        fs.Message = message;

        // Update database
        await db.UpdateAsync(fs);

        // Update local cache
        lock (shardLock)
        {
            var streams = GetLocalGuildStreams(fs.CreateKey(), guildId);

            // Message doesn't participate in equality checking
            // Removing and adding = update
            streams.Remove(fs);
            streams.Add(fs);
        }

        return (true, fs);
    }
}