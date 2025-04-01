using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest.Entities.Tracks;
using Mewdeko.Common.Configs;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Services.Strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpotifyAPI.Web;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Music.CustomPlayer;

/// <summary>
///     Custom LavaLink player to be able to handle events and such, as well as auto play.
/// </summary>
public sealed class MewdekoPlayer : LavalinkPlayer
{
    private readonly IAudioService audioService;
    private readonly BotConfig config;
    private readonly IBotCredentials creds;
    private readonly HttpClient httpClient;
    private readonly IDataCache cache;
    private readonly IMessageChannel channel;
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;
    private readonly GeneratedBotStrings Strings;
    private readonly Random random = new();
    private bool isAprilFoolsJokeRunning;

    private readonly string[] soundIds = new[]
    {
        "1356473693294825603", "1356473638899159050", "1356473603775922256"
    };

    private const ulong SourceGuildId = 843489716674494475;

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
        dbProvider = properties.ServiceProvider.GetRequiredService<DbContextProvider>();
        cache = properties.ServiceProvider.GetRequiredService<IDataCache>();
        Strings = properties.ServiceProvider.GetRequiredService<GeneratedBotStrings>();
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
                            await musicChannel.SendMessageAsync("Queue is empty. Stopping.");
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
                var failedEmbed = new EmbedBuilder()
                    .WithDescription($"Failed to load track {item.Track.Title}. Removing and skipping to the next one.")
                    .WithOkColor()
                    .Build();
                await musicChannel.SendMessageAsync(embed: failedEmbed);
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
        var queue = await cache.GetMusicQueue(GuildId);
        var currentTrack = await cache.GetCurrentTrack(GuildId);
        var musicChannel = await GetMusicChannel();

        // Create embed and component buttons
        var embed = await PrettyNowPlayingAsync(queue);
        var components = CreatePlayerControls();

        var message = await musicChannel.SendMessageAsync(embed: embed, components: components);

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
                        var chan = guild.GetVoiceChannel(VoiceChannelId);

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
    ///     Creates the player control buttons.
    /// </summary>
    /// <returns>Built component collection.</returns>
    private MessageComponent CreatePlayerControls()
    {
        var isPaused = State == PlayerState.Paused;

        return new ComponentBuilder()
            // Row 1 - Main controls
            .WithButton(customId: $"music:prev:{GuildId}", emote: new Emoji("⏮️"), style: ButtonStyle.Secondary)
            .WithButton(customId: $"music:playpause:{GuildId}", emote: new Emoji(isPaused ? "▶️" : "⏸️"),
                style: ButtonStyle.Primary)
            .WithButton(customId: $"music:next:{GuildId}", emote: new Emoji("⏭️"), style: ButtonStyle.Secondary)
            .WithButton(customId: $"music:stop:{GuildId}", emote: new Emoji("⏹️"), style: ButtonStyle.Danger)
            // Row 2 - Additional controls
            .WithButton(customId: $"music:loop:{GuildId}", emote: new Emoji("🔁"), style: ButtonStyle.Secondary, row: 1)
            .WithButton(customId: $"music:volume_down:{GuildId}", emote: new Emoji("🔉"), style: ButtonStyle.Secondary,
                row: 1)
            .WithButton(customId: $"music:volume_up:{GuildId}", emote: new Emoji("🔊"), style: ButtonStyle.Secondary,
                row: 1)
            .WithButton(customId: $"music:queue:{GuildId}", label: "Queue", style: ButtonStyle.Secondary, row: 1)
            .Build();
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

        await using var dbContext = await dbProvider.GetContextAsync();
        await dbContext.SaveChangesAsync();
        await cache.SetMusicPlayerSettings(GuildId, settings);
    }

    private async Task<MusicPlayerSettings> GetMusicSettings()
    {
        var guildId = GuildId;
        var settings = await cache.GetMusicPlayerSettings(guildId);
        if (settings is not null)
            return settings;

        await using var dbContext = await dbProvider.GetContextAsync();
        settings = await dbContext.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings is null)
        {
            settings = new MusicPlayerSettings
            {
                GuildId = guildId
            };
            await dbContext.MusicPlayerSettings.AddAsync(settings);
            await dbContext.SaveChangesAsync();
        }

        await cache.SetMusicPlayerSettings(guildId, settings);
        return settings;
    }

    /// <summary>
    ///     Sets the DJ role for the guild.
    /// </summary>
    public async Task SetDjRole(ulong? roleId)
    {
        var settings = await GetMusicSettings();
        settings.DjRoleId = roleId;

        await using var dbContext = await dbProvider.GetContextAsync();
        await dbContext.SaveChangesAsync();
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
    public async Task<Embed> PrettyNowPlayingAsync(List<MewdekoTrack> queue)
    {
        var currentTrack = await cache.GetCurrentTrack(GuildId);
        var position = Position.Value.Position;
        var duration = CurrentTrack.Duration;
        var (progressBar, percentage) = CreateProgressBar(position, duration);
        await GetMusicSettings();

        var description = new StringBuilder()
            .AppendLine("## 📀 Track Info")
            .AppendLine($"### [{currentTrack.Track.Title}]({currentTrack.Track.Uri})")
            .AppendLine()
            .AppendLine($"🎵 **Artist:** {currentTrack.Track.Author}")
            .AppendLine($"🎧 **Source:** {currentTrack.Track.Provider}")
            .AppendLine($"👤 **Requested by:** {currentTrack.Requester.Username}")
            .AppendLine()
            .AppendLine("## ⏳ Progress")
            .AppendLine(progressBar)
            .AppendLine($"`{position:hh\\:mm\\:ss}/{duration:hh\\:mm\\:ss} ({percentage:F1}%)`");

        var activeEffects = GetActiveEffects();
        if (activeEffects.Any())
        {
            description.AppendLine()
                .AppendLine("## 🎚️ Active Effects")
                .AppendLine(string.Join(" ", activeEffects));
        }

        var stats = GetPlayerStats(currentTrack.Index, queue.Count);
        if (stats.Any())
        {
            description.AppendLine()
                .AppendLine("## ℹ️ Player Stats")
                .AppendLine(string.Join("\n", stats));
        }

        var color = GetColorForPercentage(position.TotalMilliseconds / duration.TotalMilliseconds);

        var eb = new EmbedBuilder()
            .WithTitle($"🎵 Now Playing {GetRepeatEmoji()}")
            .WithDescription(description.ToString())
            .WithColor(color)
            .WithThumbnailUrl(currentTrack.Track.ArtworkUri?.ToString())
            .WithFooter(GetVolumeIndicator());

        return eb.Build();
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
            effects.Add("🎵 Bass");

        if (Filters.Timescale != null)
        {
            var speed = Filters.Timescale.Speed;
            switch (speed)
            {
                case > 1.0f:
                    effects.Add("⚡ Nightcore");
                    break;
                case < 1.0f:
                    effects.Add("🌊 Vaporwave");
                    break;
            }
        }

        if (Filters.Karaoke != null) effects.Add("🎤 Karaoke");
        if (Filters.Tremolo != null) effects.Add("〰️ Tremolo");
        if (Filters.Vibrato != null) effects.Add("📳 Vibrato");
        if (Filters.Rotation != null) effects.Add("🎧 8D");
        if (Filters.Distortion != null) effects.Add("🔊 Distort");
        if (Filters.ChannelMix != null) effects.Add("🔀 Stereo");

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

    private (string Bar, double Percentage) CreateProgressBar(TimeSpan position, TimeSpan duration)
    {
        const int barLength = 25;
        var progress = position.TotalMilliseconds / duration.TotalMilliseconds;
        var progressBarPosition = (int)(progress * barLength);
        var percentage = progress * 100;

        var bar = new StringBuilder();

        // Add start cap
        bar.Append('╠');

        // Build progress bar
        for (var i = 0; i < barLength; i++)
        {
            if (i == progressBarPosition)
                bar.Append("🔘");
            else if (i < progressBarPosition)
                bar.Append('═');
            else
                bar.Append('─');
        }

        // Add end cap
        bar.Append('╣');

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

            // Use existing Spotify client
            var spotifyClient = await GetSpotifyClient();

            // Search for the track on Spotify
            var searchRequest = new SearchRequest(SearchRequest.Types.Track, $"{trackTitle} {artistName}");
            var searchResults = await spotifyClient.Search.Item(searchRequest);

            // If no results found, try with just the track title
            if (searchResults.Tracks.Items.Count == 0)
            {
                searchRequest = new SearchRequest(SearchRequest.Types.Track, trackTitle);
                searchResults = await spotifyClient.Search.Item(searchRequest);

                // If still no results, return
                if (searchResults.Tracks.Items.Count == 0)
                    return true;
            }

            // Get recommendations based on seed track
            var seedTrack = searchResults.Tracks.Items[0];

            // Create recommendation request with enhanced parameters
            var recommendationsRequest = new RecommendationsRequest();

            // Add seed track
            recommendationsRequest.SeedTracks.Add(seedTrack.Id);

            // If we have space, add seed artists (up to 2, ensuring we don't exceed 5 total seeds)
            foreach (var artist in seedTrack.Artists.Take(Math.Min(4, seedTrack.Artists.Count)))
            {
                recommendationsRequest.SeedArtists.Add(artist.Id);

                // Break if we've reached max of 5 seed values
                if (recommendationsRequest.SeedArtists.Count + recommendationsRequest.SeedTracks.Count >= 5)
                    break;
            }

            // Set limit to number of tracks to autoplay
            recommendationsRequest.Limit = autoPlay;

            // Get recommendations
            var recommendations = await spotifyClient.Browse.GetRecommendations(recommendationsRequest);

            // Filter out tracks that are already in the queue
            var queuedTrackNames =
                new HashSet<string>(queue.Select(q => q.Track.Title), StringComparer.OrdinalIgnoreCase);
            var filteredTracks = recommendations.Tracks
                .Where(t => !queuedTrackNames.Contains(t.Name))
                .ToList();

            var toTake = Math.Min(autoPlay, filteredTracks.Count);

            foreach (var track in filteredTracks.Take(toTake))
            {
                // Create search query with track name and primary artist
                var artistsString = string.Join(", ", track.Artists.Take(1).Select(a => a.Name));
                var searchQuery = $"{track.Name} {artistsString}";

                var trackToLoad = await audioService.Tracks.LoadTrackAsync(searchQuery, TrackSearchMode.YouTube);
                if (trackToLoad is null)
                    continue;

                queue.Add(new MewdekoTrack(queue.Count + 1, trackToLoad, new PartialUser
                {
                    AvatarUrl = client.CurrentUser.GetAvatarUrl(), Username = "Mewdeko", Id = client.CurrentUser.Id
                }));
            }

            await cache.SetMusicQueue(GuildId, queue);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Spotify AutoPlay error: {e.Message}");
            // Fall back to default behavior if Spotify fails
            return true;
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
            artistName = fullTitle.Substring(0, hyphenIndex).Trim();
            trackTitle = fullTitle.Substring(hyphenIndex + 3).Trim();
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


    /// <summary>
    ///     Sets the volume for the player.
    /// </summary>
    /// <param name="volume">The volume to set.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task SetGuildVolumeAsync(int volume)
    {
        var settings = await GetMusicSettings();
        settings.Volume = volume;

        await using var dbContext = await dbProvider.GetContextAsync();
        await dbContext.SaveChangesAsync();
        await cache.SetMusicPlayerSettings(GuildId, settings);
    }

    /// <summary>
    ///     Sets settings for music.
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="settings"></param>
    public async Task SetMusicSettings(ulong guildId, MusicPlayerSettings settings)
    {
        await using var db = await dbProvider.GetContextAsync();
        db.MusicPlayerSettings.Update(settings);
        await cache.SetMusicPlayerSettings(guildId, settings);
        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     Gets the repeat type for the player.
    /// </summary>
    /// <returns>A <see cref="PlayerRepeatType" /> for the guild.</returns>
    public async Task<PlayerRepeatType> GetRepeatType()
    {
        var settings = await GetMusicSettings();
        return settings.PlayerRepeat;
    }

    /// <summary>
    ///     Sets the repeat type for the player.
    /// </summary>
    /// <param name="repeatType">The repeat type to set.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task SetRepeatTypeAsync(PlayerRepeatType repeatType)
    {
        var settings = await GetMusicSettings();
        settings.PlayerRepeat = repeatType;

        await using var dbContext = await dbProvider.GetContextAsync();
        await dbContext.SaveChangesAsync();
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

        await using var dbContext = await dbProvider.GetContextAsync();
        await dbContext.SaveChangesAsync();
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
                sound_id = soundId, source_guild_id = SourceGuildId.ToString()
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