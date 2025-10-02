using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Hqub.Lastfm;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest.Entities.Tracks;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.DependencyInjection;
using SpotifyAPI.Web;

namespace Mewdeko.Modules.Music.CustomPlayer;

/// <summary>
///     Custom LavaLink player to be able to handle events and such, as well as auto play.
/// </summary>
public sealed class MewdekoPlayer : LavalinkPlayer
{
    private const ulong SourceGuildId = 843489716674494475;
    private readonly IAudioService audioService;
    private readonly IDataCache cache;
    private readonly IMessageChannel channel;
    private readonly DiscordShardedClient client;
    private readonly BotConfig config;
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly HttpClient httpClient;
    private readonly ILogger<MewdekoPlayer> logger;
    private readonly Random random = new();

    private readonly string[] soundIds =
    [
        "1356473693294825603", "1356473638899159050", "1356473603775922256"
    ];

    private readonly PlayerStateTracker stateTracker;

    private readonly GeneratedBotStrings Strings;
    private bool isAprilFoolsJokeRunning;

    /// <summary>
    ///     Initializes a new instance of <see cref="MewdekoPlayer" />.
    /// </summary>
    /// <param name="properties">The player properties.</param>
    public MewdekoPlayer(IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties) : base(properties)
    {
        httpClient = properties.ServiceProvider.GetRequiredService<HttpClient>();
        config = properties.ServiceProvider.GetRequiredService<BotConfig>();
        audioService = properties.ServiceProvider.GetRequiredService<IAudioService>();
        creds = properties.ServiceProvider.GetRequiredService<IBotCredentials>();
        channel = properties.Options.Value.Channel;
        client = properties.ServiceProvider.GetRequiredService<DiscordShardedClient>();
        dbFactory = properties.ServiceProvider.GetRequiredService<IDataConnectionFactory>();
        cache = properties.ServiceProvider.GetRequiredService<IDataCache>();
        Strings = properties.ServiceProvider.GetRequiredService<GeneratedBotStrings>();
        logger = properties.ServiceProvider.GetRequiredService<ILogger<MewdekoPlayer>>();
        stateTracker = new PlayerStateTracker(this, cache);
    }


    /// <summary>
    ///     Handles the event the track ended, resolves stuff like auto play, auto playing the next track, and looping.
    /// </summary>
    /// <param name="item">The ended track.</param>
    /// <param name="reason">The reason the track ended.</param>
    /// <param name="token">The cancellation token.</param>
    protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem item, TrackEndReason reason,
        CancellationToken token = default)
    {
        if (stateTracker != null && State is PlayerState.Playing or PlayerState.Paused)
            await stateTracker.ForceUpdate();
        var musicChannel = await GetMusicChannel();
        var queue = await cache.GetMusicQueue(GuildId);
        var currentTrack = await cache.GetCurrentTrack(GuildId);
        var nextTrack = queue.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
        switch (reason)
        {
            case TrackEndReason.Finished:
                var repeatType = await GetRepeatType();
                switch (repeatType)
                {
                    case PlayerRepeatType.None:

                        if (nextTrack is null)
                        {
                            await musicChannel.SendMessageAsync(Strings.QueueEmpty(GuildId));
                            await StopAsync(token);
                            await cache.SetCurrentTrack(GuildId, null);
                        }
                        else
                        {
                            await PlayAsync(nextTrack.Track, cancellationToken: token);
                            await cache.SetCurrentTrack(GuildId, nextTrack);
                        }

                        break;
                    case PlayerRepeatType.Track:
                        await PlayAsync(item.Track, cancellationToken: token);
                        break;
                    case PlayerRepeatType.Queue:
                        if (nextTrack is null)
                        {
                            await PlayAsync(queue[0].Track, cancellationToken: token);
                            await cache.SetCurrentTrack(GuildId, queue[0]);
                        }
                        else
                        {
                            await PlayAsync(nextTrack.Track, cancellationToken: token);
                            await cache.SetCurrentTrack(GuildId, nextTrack);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                break;
            case TrackEndReason.LoadFailed:
                var components = new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder("# ⚠️ Track Load Failed")
                    ], Mewdeko.ErrorColor)
                    .WithSeparator()
                    .WithContainer(new TextDisplayBuilder(Strings.TrackLoadFailed(GuildId, item.Track.Title)));
                await musicChannel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                    allowedMentions: AllowedMentions.None);
                await PlayAsync(nextTrack.Track, cancellationToken: token);
                await cache.SetCurrentTrack(GuildId, nextTrack);
                queue.Remove(currentTrack);
                await cache.SetMusicQueue(GuildId, queue);
                break;
            case TrackEndReason.Stopped:
                return;
            case TrackEndReason.Replaced:
                break;
            case TrackEndReason.Cleanup:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }
    }

    /// <summary>
    ///     Notifies the channel that a track has started playing.
    /// </summary>
    /// <param name="track">The track that started playing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track,
    CancellationToken cancellationToken = default)
    {
        await stateTracker.ForceUpdate();
        var queue = await cache.GetMusicQueue(GuildId);
        var currentTrack = await cache.GetCurrentTrack(GuildId);
        var musicChannel = await GetMusicChannel();

        if (queue == null || currentTrack == null || musicChannel == null)
        {
            var nullParts = new List<string>();
            if (queue == null)
                nullParts.Add("queue");
            if (currentTrack == null)
                nullParts.Add("currentTrack");
            if (musicChannel == null)
                nullParts.Add("musicChannel");
            logger?.LogError("NotifyTrackStartedAsync: {NullParts} is/are null. GuildId: {GuildId}", string.Join(", ", nullParts), GuildId);
            return;
        }

        // Create now playing display with integrated control buttons
        var nowPlayingComponents = await PrettyNowPlayingAsync(queue);

        var message = await musicChannel.SendMessageAsync(components: nowPlayingComponents,
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);

        if (DateTime.Now.Month == 4 && DateTime.Now.Day == 1 && !isAprilFoolsJokeRunning)
        {
            if (random.Next(100) < 30)
            {
                isAprilFoolsJokeRunning = true;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var guild = client.GetGuild(GuildId);
                        var chan = guild?.GetVoiceChannel(VoiceChannelId);

                        if (chan != null)
                        {
                            await Task.Delay(random.Next(30000, 120000), cancellationToken);

                            await PlaySoundboardEffect(VoiceChannelId, soundIds[0]);

                            await Task.Delay(random.Next(15000, 45000), cancellationToken);

                            await PlaySoundboardEffect(VoiceChannelId, soundIds[1]);

                            await Task.Delay(random.Next(20000, 60000), cancellationToken);

                            await PlaySoundboardEffect(VoiceChannelId, soundIds[2]);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"April Fools prank error: {ex.Message}");
                    }
                    finally
                    {
                        isAprilFoolsJokeRunning = false;
                    }
                }, cancellationToken);
            }
        }

        if (currentTrack?.Index == queue.Count)
        {
            var success = await AutoPlay();
            if (!success)
            {
                await musicChannel.SendErrorAsync(Strings.LastfmCredentialsInvalidAutoplay(GuildId), config);
                await SetAutoPlay(0);
            }
        }
    }


    /// <summary>
    ///     Gets the music channel for the player.
    /// </summary>
    /// <returns>The music channel for the player.</returns>
    public async Task<IMessageChannel?> GetMusicChannel()
    {
        var settings = await GetMusicSettings();
        return settings.MusicChannelId.HasValue
            ? client.GetGuild(GuildId)?.GetTextChannel(settings.MusicChannelId.Value)
            : channel;
    }

    /// <summary>
    ///     Sets the music channel for the player.
    /// </summary>
    /// <param name="channelId">The channel id to set.</param>
    public async Task SetMusicChannelAsync(ulong channelId)
    {
        var settings = await GetMusicSettings();
        settings.MusicChannelId = channelId;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(settings);
        await cache.SetMusicPlayerSettings(GuildId, settings);
    }

    private async Task<MusicPlayerSetting> GetMusicSettings()
    {
        var guildId = GuildId;
        var settings = await cache.GetMusicPlayerSettings(guildId);
        if (settings is not null)
            return settings;

        await using var db = await dbFactory.CreateConnectionAsync();
        settings = await db.MusicPlayerSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings is null)
        {
            settings = new MusicPlayerSetting
            {
                GuildId = guildId
            };
            await db.InsertAsync(settings);
        }

        await cache.SetMusicPlayerSettings(guildId, settings);
        return settings;
    }

    /// <summary>
    ///     Sets the DJ role for the guild.
    /// </summary>
    /// <param name="roleId">The roleid identifier.</param>
    public async Task SetDjRole(ulong? roleId)
    {
        var settings = await GetMusicSettings();
        settings.DjRoleId = roleId;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(settings);
        await cache.SetMusicPlayerSettings(GuildId, settings);
    }

    /// <summary>
    ///     Checks if a user has DJ permissions.
    /// </summary>
    public async Task<bool> HasDjAsync(IGuildUser user)
    {
        if (user.GuildPermissions.Administrator)
            return true;

        var settings = await GetMusicSettings();
        if (!settings.DjRoleId.HasValue)
            return false;

        return user.RoleIds.Contains(settings.DjRoleId.Value);
    }

    /// <summary>
    ///     Gets the DJ role for the guild.
    /// </summary>
    public async Task<ulong?> GetDjRole()
    {
        var settings = await GetMusicSettings();
        return settings.DjRoleId;
    }

    /// <summary>
    ///     Gets a pretty now playing message for the player.
    /// </summary>
    /// <summary>
    ///     Gets a pretty now playing message for the player.
    /// </summary>
    public async Task<MessageComponent> PrettyNowPlayingAsync(List<MewdekoTrack> queue, bool minimal = false)
    {
        var currentTrack = await cache.GetCurrentTrack(GuildId);
        var position = Position.Value.Position;
        var duration = CurrentTrack.Duration;
        var (progressBar, percentage) = CreateProgressBar(position, duration);
        var settings = await GetMusicSettings();

        var color = GetColorForPercentage(position.TotalMilliseconds / duration.TotalMilliseconds);
        var containerComponents = new List<IMessageComponentBuilder>();

        // Clean header without extra emojis
        containerComponents.Add(new TextDisplayBuilder()
            .WithContent("# Now Playing"));

        containerComponents.Add(new SeparatorBuilder());

        // Main track info section with artwork
        var trackSection = new SectionBuilder()
            .WithComponents([
                new TextDisplayBuilder($"## [{currentTrack.Track.Title}]({currentTrack.Track.Uri})\n" +
                                       $"{currentTrack.Track.Author}\n" +
                                       $"🎵 {currentTrack.Track.Provider} • 👤 {currentTrack.Requester.Username}")
            ]);

        if (currentTrack.Track.ArtworkUri != null)
        {
            var thumbnailBuilder = new ThumbnailBuilder()
                .WithMedia(new UnfurledMediaItemProperties
                {
                    Url = currentTrack.Track.ArtworkUri.ToString()
                });
            trackSection.WithAccessory(thumbnailBuilder);
        }

        containerComponents.Add(trackSection);
        containerComponents.Add(new SeparatorBuilder());

        // Progress bar section - spans full width
        containerComponents.Add(new TextDisplayBuilder()
            .WithContent($"`{position:mm\\:ss}` {progressBar} `{duration:mm\\:ss}`"));

        containerComponents.Add(new SeparatorBuilder());

        // Main playback controls row
        var isPaused = State == PlayerState.Paused;
        var mainControlsRow = new ActionRowBuilder()
            .WithButton(customId: $"music:loop:{GuildId}", emote: new Emoji("🔁"), style: ButtonStyle.Secondary)
            .WithButton(customId: $"music:prev:{GuildId}", emote: new Emoji("⏮️"), style: ButtonStyle.Secondary)
            .WithButton(customId: $"music:playpause:{GuildId}", emote: new Emoji(isPaused ? "▶️" : "⏸️"),
                style: ButtonStyle.Primary)
            .WithButton(customId: $"music:next:{GuildId}", emote: new Emoji("⏭️"), style: ButtonStyle.Secondary)
            .WithButton(customId: $"music:stop:{GuildId}", emote: new Emoji("⏹️"), style: ButtonStyle.Danger);

        containerComponents.Add(mainControlsRow);
        containerComponents.Add(new SeparatorBuilder());

        // Enhanced footer with more detailed information
        var activeEffects = GetActiveEffects();
        var effectsText = activeEffects.Any() ? $" • 🎛️ {string.Join(", ", activeEffects)}" : "";
        var repeatEmoji = GetRepeatEmoji();
        var volumeEmoji = GetVolumeEmoji();
        var repeatText = repeatEmoji != "" ? $" • {repeatEmoji} Repeat" : "";

        var footerText =
            $"{volumeEmoji} **{Volume * 100:0}%** • 📑 **{currentTrack.Index}** of **{queue.Count}**{repeatText}{effectsText}";

        containerComponents.Add(new TextDisplayBuilder()
            .WithContent(footerText));

        // Secondary controls at the bottom
        containerComponents.Add(new SeparatorBuilder());

        var secondaryControlsRow = new ActionRowBuilder()
            .WithButton(customId: $"music:volume_down:{GuildId}", emote: new Emoji("🔉"), style: ButtonStyle.Secondary)
            .WithButton(customId: $"music:volume_up:{GuildId}", emote: new Emoji("🔊"), style: ButtonStyle.Secondary)
            .WithButton(customId: $"music:queue:{GuildId}", label: "View Queue", style: ButtonStyle.Secondary);

        containerComponents.Add(secondaryControlsRow);

        // Create the main container
        var mainContainer = new ContainerBuilder()
            .WithComponents(containerComponents)
            .WithAccentColor(color);

        var componentsV2 = new ComponentBuilderV2()
            .AddComponent(mainContainer);

        return componentsV2.Build();
    }

    private string GetVolumeIndicator()
    {
        var volume = Volume * 100;
        var icon = volume switch
        {
            0 => "🔇",
            <= 33 => "🔈",
            <= 67 => "🔉",
            _ => "🔊"
        };
        return $"{icon} Volume: {volume}%";
    }

    private List<string> GetActiveEffects()
    {
        var effects = new List<string>();

        if (Filters.Equalizer != null)
            effects.Add("Bass");

        if (Filters.Timescale != null)
        {
            var speed = Filters.Timescale.Speed;
            switch (speed)
            {
                case > 1.0f:
                    effects.Add("Nightcore");
                    break;
                case < 1.0f:
                    effects.Add("Vaporwave");
                    break;
            }
        }

        if (Filters.Karaoke != null) effects.Add("Karaoke");
        if (Filters.Tremolo != null) effects.Add("Tremolo");
        if (Filters.Vibrato != null) effects.Add("Vibrato");
        if (Filters.Rotation != null) effects.Add("8D");
        if (Filters.Distortion != null) effects.Add("Distort");
        if (Filters.ChannelMix != null) effects.Add("Stereo");

        return effects;
    }

    private List<string> GetPlayerStats(int currentIndex, int totalTracks)
    {
        var stats = new List<string>
        {
            $"📑 Track **{currentIndex}** of **{totalTracks}**",
            $"🎚️ Volume at **{Volume * 100}%**",
            $"🔁 Repeat mode: **{GetRepeatType().GetAwaiter().GetResult()}**"
        };

        return stats;
    }

    private string GetRepeatEmoji()
    {
        return GetRepeatType().GetAwaiter().GetResult() switch
        {
            PlayerRepeatType.None => "",
            PlayerRepeatType.Track => "🔂",
            PlayerRepeatType.Queue => "🔁",
            _ => ""
        };
    }

    private string GetVolumeEmoji()
    {
        var volume = Volume * 100;
        return volume switch
        {
            0 => "🔇",
            <= 33 => "🔈",
            <= 67 => "🔉",
            _ => "🔊"
        };
    }

    private (string Bar, double Percentage) CreateProgressBar(TimeSpan position, TimeSpan duration)
    {
        const int barLength = 20;
        var progress = position.TotalMilliseconds / duration.TotalMilliseconds;
        var progressBarPosition = (int)(progress * barLength);
        var percentage = progress * 100;

        var bar = new StringBuilder();

        // Build progress bar with cleaner style
        for (var i = 0; i < barLength; i++)
        {
            if (i == progressBarPosition)
                bar.Append("●"); // Current position indicator
            else if (i < progressBarPosition)
                bar.Append("━"); // Completed portion
            else
                bar.Append("─"); // Remaining portion
        }

        return (bar.ToString(), percentage);
    }

    private static Color GetColorForPercentage(double percentage)
    {
        // Interpolate between colors based on progress
        if (percentage < 0.5)
        {
            // Interpolate from blue to purple
            var t = percentage * 2;
            return new Color(
                (byte)(147 * t + 88 * (1 - t)),
                (byte)(112 * t + 101 * (1 - t)),
                (byte)(219 * t + 242 * (1 - t))
            );
        }
        else
        {
            // Interpolate from purple to pink
            var t = (percentage - 0.5) * 2;
            return new Color(
                (byte)(147 * (1 - t) + 255 * t),
                (byte)(112 * (1 - t) + 192 * t),
                (byte)(219 * (1 - t) + 203 * t)
            );
        }
    }

    private async Task<string> GetQueueInfoAsync(List<MewdekoTrack> queue)
    {
        var settings = await GetMusicSettings();
        var totalDuration = TimeSpan.FromMilliseconds(queue.Sum(x => x.Track.Duration.TotalMilliseconds));

        return $"Queue: {queue.Count} tracks | {totalDuration:hh\\:mm\\:ss} total" +
               (settings.VoteSkipEnabled ? $" | Vote Skip: {settings.VoteSkipThreshold}% needed" : "");
    }

    /// <summary>
    ///     Contains logic for handling autoplay in a server using Spotify's recommendation system.
    /// </summary>
    /// <returns>A bool indicating if the operation was successful.</returns>
    /// <summary>
    ///     Contains logic for handling autoplay in a server using Last.fm's similar tracks API.
    /// </summary>
    /// <returns>A bool indicating if the operation was successful.</returns>
    public async Task<bool> AutoPlay()
    {
        try
        {
            var autoPlay = await GetAutoPlay();
            if (autoPlay == 0)
                return true;

            var queue = await cache.GetMusicQueue(GuildId);
            var lastSong = queue.MaxBy(x => x.Index);
            if (lastSong is null)
                return true;

            // Extract track and artist information
            var (artistName, trackTitle) = ExtractTrackInfo(lastSong.Track.Title, lastSong.Track.Author);

            // Initialize Last.fm client using API key from credentials
            if (string.IsNullOrEmpty(creds.LastFmApiKey))
            {
                logger.LogWarning("Last.fm API key is not configured. AutoPlay cannot function.");
                return false;
            }

            var lastfmClient = new LastfmClient(creds.LastFmApiKey);

            // Get similar tracks from Last.fm
            var similarTracks = await lastfmClient.Track.GetSimilarAsync(trackTitle, artistName, autoPlay * 2);
            if (similarTracks == null || !similarTracks.Any())
            {
                logger.LogWarning($"No similar tracks found for {trackTitle} by {artistName}");
                return true;
            }

            // Filter out tracks that are already in the queue
            var queuedTrackNames = new HashSet<string>(
                queue.Select(q => q.Track.Title.ToLower()),
                StringComparer.OrdinalIgnoreCase);

            var filteredTracks = similarTracks
                .Where(t => !queuedTrackNames.Contains($"{t.Name} - {t.Artist.Name}".ToLower()))
                .ToList();

            var toTake = Math.Min(autoPlay, filteredTracks.Count);

            logger.LogInformation($"Last.fm AutoPlay found {filteredTracks.Count} potential tracks, adding {toTake}");

            foreach (var track in filteredTracks.Take(toTake))
            {
                // Create search query with track name and artist
                var searchQuery = $"{track.Name} {track.Artist.Name}";

                var trackToLoad = await audioService.Tracks.LoadTrackAsync(searchQuery, TrackSearchMode.YouTube);
                if (trackToLoad is null)
                {
                    logger.LogDebug($"Could not load track: {searchQuery}");
                    continue;
                }

                queue.Add(new MewdekoTrack(queue.Count + 1, trackToLoad, new PartialUser
                {
                    AvatarUrl = client.CurrentUser.GetAvatarUrl(),
                    Username = "Mewdeko (Last.fm AutoPlay)",
                    Id = client.CurrentUser.Id
                }));

                logger.LogDebug($"Added track to queue: {trackToLoad.Title}");
            }

            await cache.SetMusicQueue(GuildId, queue);

            // If we've added tracks, return success
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Last.fm AutoPlay error");
            return false;
        }
    }

    /// <summary>
    ///     Extracts the artist name and track title from a song's metadata.
    /// </summary>
    /// <param name="fullTitle">The full track title</param>
    /// <param name="authorFromTrack">The author information</param>
    /// <returns>A tuple containing the artist name and track title</returns>
    private (string artistName, string trackTitle) ExtractTrackInfo(string fullTitle, string authorFromTrack)
    {
        var trackTitle = fullTitle;
        var artistName = authorFromTrack;
        var hyphenIndex = fullTitle.IndexOf(" - ", StringComparison.Ordinal);

        // If the title has a hyphen, split the title and artist
        if (hyphenIndex != -1)
        {
            artistName = fullTitle[..hyphenIndex].Trim();
            trackTitle = fullTitle[(hyphenIndex + 3)..].Trim();
        }

        // Clean up the track title by removing extra information
        trackTitle = Regex.Replace(trackTitle, @"\s*\[.*?\]\s*", "", RegexOptions.Compiled);
        trackTitle = Regex.Replace(trackTitle, @"\s*\([^)]*\)\s*", "", RegexOptions.Compiled);
        trackTitle = trackTitle.Trim();

        return (artistName, trackTitle);
    }

    /// <summary>
    ///     Gets the volume for a guild, defaults to max.
    /// </summary>
    /// <returns>An integer representing the guilds player volume</returns>
    public async Task<int> GetVolume()
    {
        var settings = await GetMusicSettings();
        return settings.Volume;
    }


    /// <inheritdoc />
    public override async ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        await base.SeekAsync(position, cancellationToken);

        var state = new MusicPlayerState
        {
            GuildId = GuildId,
            VoiceChannelId = VoiceChannelId,
            CurrentPosition = position,
            IsPlaying = State == PlayerState.Playing,
            IsPaused = State == PlayerState.Paused,
            Volume = Volume,
            LastUpdateTime = DateTime.UtcNow
        };

        await cache.SetPlayerState(GuildId, state);
    }

    /// <summary>
    ///     Sets the volume for the player.
    /// </summary>
    /// <param name="volume">The volume to set.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task SetGuildVolumeAsync(int volume)
    {
        var settings = await GetMusicSettings();
        settings.Volume = volume;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(settings);
        await cache.SetMusicPlayerSettings(GuildId, settings);
    }

    /// <summary>
    ///     Sets settings for music.
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="settings"></param>
    public async Task SetMusicSettings(ulong guildId, MusicPlayerSetting settings)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(settings);
        await cache.SetMusicPlayerSettings(guildId, settings);
    }

    /// <summary>
    ///     Gets the repeat type for the player.
    /// </summary>
    /// <returns>A <see cref="PlayerRepeatType" /> for the guild.</returns>
    public async Task<PlayerRepeatType> GetRepeatType()
    {
        var settings = await GetMusicSettings();
        return (PlayerRepeatType)settings.PlayerRepeat;
    }

    /// <summary>
    ///     Sets the repeat type for the player.
    /// </summary>
    /// <param name="repeatType">The repeat type to set.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task SetRepeatTypeAsync(PlayerRepeatType repeatType)
    {
        var settings = await GetMusicSettings();
        settings.PlayerRepeat = (int)repeatType;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(settings);
        await cache.SetMusicPlayerSettings(GuildId, settings);
    }

    /// <summary>
    ///     Gets the autoplay number for a guild, usually off.
    /// </summary>
    public async Task<int> GetAutoPlay()
    {
        var settings = await GetMusicSettings();
        return settings.AutoPlay;
    }

    /// <summary>
    ///     Sets the autoplay amount for the guild.
    /// </summary>
    /// <param name="autoPlay">The amount of songs to autoplay.</param>
    public async Task SetAutoPlay(int autoPlay)
    {
        var settings = await GetMusicSettings();
        settings.AutoPlay = autoPlay;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(settings);
        await cache.SetMusicPlayerSettings(GuildId, settings);
    }

    /// <summary>
    ///     Gets a spotify client for the bot if the spotify api key is valid.
    /// </summary>
    /// <returns>A SpotifyClient</returns>
    public async Task<SpotifyClient> GetSpotifyClient()
    {
        var spotifyClientConfig = SpotifyClientConfig.CreateDefault();
        var request =
            new ClientCredentialsRequest(creds.SpotifyClientId, creds.SpotifyClientSecret);
        var response = await new OAuthClient(spotifyClientConfig).RequestToken(request).ConfigureAwait(false);
        return new SpotifyClient(spotifyClientConfig.WithToken(response.AccessToken));
    }

    private async Task PlaySoundboardEffect(ulong channelId, string soundId)
    {
        try
        {
            var playSoundUrl = $"https://discord.com/api/v10/channels/{channelId}/send-soundboard-sound";

            var playSoundRequest = new HttpRequestMessage(HttpMethod.Post, playSoundUrl);
            playSoundRequest.Headers.Add("Authorization", $"Bot {creds.Token}");

            var payload = new
            {
                sound_id = soundId,
                source_guild_id = SourceGuildId.ToString()
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            playSoundRequest.Content = new StringContent(
                jsonContent,
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.SendAsync(playSoundRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to play soundboard effect: {response.StatusCode}, Error: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error playing soundboard effect: {ex.Message}");
        }
    }
}