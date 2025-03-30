using System.Text.Json;
using Lavalink4NET;
using Lavalink4NET.Filters;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing music playback and settings
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
public class MusicController : Controller
{
    private readonly IAudioService audioService;
    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly MusicEventManager eventManager;

    /// <summary>
    ///     Controller for managing music playback and settings
    /// </summary>
    /// <param name="audioService">The audio service for managing music playback operations</param>
    /// <param name="cache">The data cache for storing and retrieving music-related information</param>
    /// <param name="client">The Discord client for accessing guild and user information</param>
    /// <param name="eventManager">The event manager for music events</param>
    public MusicController(
        IAudioService audioService,
        IDataCache cache,
        DiscordShardedClient client,
        MusicEventManager eventManager)
    {
        this.audioService = audioService;
        this.cache = cache;
        this.client = client;
        this.eventManager = eventManager;
    }

    /// <summary>
    ///     Gets the current player state and track information
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The Discord user ID making the request</param>
    /// <returns>Player status including current track, queue, and settings</returns>
    [HttpGet("status")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetPlayerStatus(ulong guildId, [FromQuery] ulong userId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        var botVoiceChannel = player.VoiceChannelId;
        var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel;

        var currentTrack = await cache.GetCurrentTrack(guildId);
        var queue = await cache.GetMusicQueue(guildId);
        var settings = await cache.GetMusicPlayerSettings(guildId);

        var status = new
        {
            CurrentTrack = currentTrack,
            Queue = queue,
            player.State,
            player.Volume,
            player.Position,
            RepeatMode = settings.PlayerRepeat,
            Filters = new
            {
                BassBoost = player.Filters.Equalizer != null,
                Nightcore = player.Filters.Timescale?.Speed > 1.0f,
                Vaporwave = player.Filters.Timescale?.Speed < 1.0f,
                Karaoke = player.Filters.Karaoke != null,
                Tremolo = player.Filters.Tremolo != null,
                Vibrato = player.Filters.Vibrato != null,
                Rotation = player.Filters.Rotation != null,
                Distortion = player.Filters.Distortion != null,
                ChannelMix = player.Filters.ChannelMix != null
            },
            IsInVoiceChannel = isInVoiceChannel
        };

        return Ok(status);
    }

    /// <summary>
    ///     Searches for tracks using the provided query and search mode
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="mode">The search mode (YouTube, Spotify, SoundCloud)</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <returns>A list of matching tracks</returns>
    [HttpGet("search")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SearchTracks([FromQuery] string query, [FromQuery] string mode = "YouTube",
        [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Search query is required");

        if (limit < 1 || limit > 25)
            limit = 10;

        try
        {
            // Parse the search mode
            var searchMode = mode.ToLower() switch
            {
                "youtube" => TrackSearchMode.YouTube,
                "spotify" => TrackSearchMode.Spotify,
                "soundcloud" => TrackSearchMode.SoundCloud,
                "youtubemusic" => TrackSearchMode.YouTubeMusic,
                _ => TrackSearchMode.YouTube
            };

            // Perform the search
            var trackResults = await audioService.Tracks.LoadTracksAsync(query, new TrackLoadOptions
            {
                SearchMode = searchMode
            });

            if (!trackResults.IsSuccess)
                return Ok(new
                {
                    Tracks = Array.Empty<object>()
                });

            // Map the tracks to a response-friendly format
            var tracks = trackResults.Tracks
                .Take(limit)
                .Select(track => new
                {
                    track.Title,
                    track.Author,
                    Duration = track.Duration.ToString(@"mm\:ss"),
                    Uri = track.Uri?.ToString(),
                    ArtworkUri = track.ArtworkUri?.ToString(),
                    Provider = track.Provider.ToString()
                })
                .ToList();

            return Ok(new
            {
                Tracks = tracks
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching for tracks with query: {Query}", query);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Extracts track information from a provided URL
    /// </summary>
    /// <param name="url">The URL to extract information from</param>
    /// <returns>Track information if available</returns>
    [HttpGet("extract")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ExtractTrack([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("URL is required");

        try
        {
            // Determine the appropriate search mode based on the URL
            var searchMode = url.ToLower() switch
            {
                var u when u.Contains("spotify.com") => TrackSearchMode.Spotify,
                var u when u.Contains("music.youtube") => TrackSearchMode.YouTubeMusic,
                var u when u.Contains("youtube.com") || u.Contains("youtu.be") => TrackSearchMode.YouTube,
                var u when u.Contains("soundcloud.com") => TrackSearchMode.SoundCloud,
                _ => TrackSearchMode.None
            };

            // Load the track
            var track = await audioService.Tracks.LoadTrackAsync(url, new TrackLoadOptions
            {
                SearchMode = searchMode
            });

            if (track == null)
                return NotFound("Could not extract track information from URL");

            // Map the track to a response-friendly format
            var trackInfo = new
            {
                track.Title,
                track.Author,
                Duration = track.Duration.ToString(@"mm\:ss"),
                Uri = track.Uri?.ToString(),
                ArtworkUri = track.ArtworkUri?.ToString(),
                Provider = track.Provider.ToString()
            };

            return Ok(trackInfo);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting track info from URL: {Url}", url);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     WebSocket endpoint for real-time music updates
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The Discord user ID making the request</param>
    /// <returns>A WebSocket or SSE connection providing real-time player updates</returns>
    [HttpGet("events")]
    [AllowAnonymous]
    public async Task GetMusicEvents(ulong guildId, [FromQuery] ulong userId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            // Fall back to Server-Sent Events if WebSockets not available
            if (Request.Headers["Accept"].ToString().Contains("text/event-stream"))
            {
                await HandleServerSentEvents(guildId, userId);
                return;
            }

            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsync("WebSocket or EventSource connection required");
            return;
        }

        // Accept the WebSocket connection
        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await eventManager.HandleWebSocketConnection(guildId, userId, webSocket, HttpContext);
    }

    /// <summary>
    ///     Fallback Server-Sent Events handler
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The Discord user ID making the request</param>
    /// <returns>A task that completes when the SSE connection is closed</returns>
    private async Task HandleServerSentEvents(ulong guildId, ulong userId)
    {
        HttpContext.Response.Headers.Add("Content-Type", "text/event-stream");
        HttpContext.Response.Headers.Add("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Add("Connection", "keep-alive");

        // Send initial status
        var initialStatus = await GetInitialStatus(guildId, userId);
        var initialJson = JsonSerializer.Serialize(initialStatus);
        await HttpContext.Response.WriteAsync($"event: status\ndata: {initialJson}\n\n");
        await HttpContext.Response.Body.FlushAsync();

        // Register for updates
        var connectionId = Guid.NewGuid().ToString();
        var completion = new TaskCompletionSource<bool>();

        // Register for player updates
        eventManager.RegisterSseConnection(guildId, connectionId, async status =>
        {
            try
            {
                var json = JsonSerializer.Serialize(status);
                await HttpContext.Response.WriteAsync($"event: status\ndata: {json}\n\n");
                await HttpContext.Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending SSE update");
                completion.TrySetResult(false);
            }
        }, userId);

        // Handle disconnection
        HttpContext.RequestAborted.Register(() =>
        {
            eventManager.UnregisterSseConnection(guildId, connectionId);
            completion.TrySetResult(true);
        });

        // Keep the connection alive with heartbeats
        _ = Task.Run(async () =>
        {
            try
            {
                while (!completion.Task.IsCompleted)
                {
                    await Task.Delay(30000); // 30 second heartbeat
                    try
                    {
                        await HttpContext.Response.WriteAsync("event: heartbeat\ndata: {\"time\":" +
                                                              DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "}\n\n");
                        await HttpContext.Response.Body.FlushAsync();
                    }
                    catch
                    {
                        completion.TrySetResult(false);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in SSE heartbeat");
            }
        });

        // Wait for the connection to close
        await completion.Task;
        eventManager.UnregisterSseConnection(guildId, connectionId);
    }

    /// <summary>
    ///     Plays a specific track from the queue by its index
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="index">The index of the track to play</param>
    /// <returns>The result of the operation</returns>
    [HttpPost("play-track/{index}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> PlayTrackAt(ulong guildId, int index)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        var queue = await cache.GetMusicQueue(guildId);
        var track = queue.FirstOrDefault(t => t.Index == index);

        if (track == null)
            return NotFound("Track not found in queue");

        // Play the requested track
        await player.PlayAsync(track.Track);

        // Update the current track in cache
        await cache.SetCurrentTrack(guildId, track);

        // Notify clients of track change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            Message = $"Now playing track {index}: {track.Track.Title}", Track = track
        });
    }

    /// <summary>
    ///     Get initial player status for real-time connections
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The Discord user ID making the request</param>
    /// <returns>An object containing the current player status</returns>
    private async Task<object> GetInitialStatus(ulong guildId, ulong userId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return new
            {
                error = "No active player"
            };

        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        var botVoiceChannel = player.VoiceChannelId;
        var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel;

        var currentTrack = await cache.GetCurrentTrack(guildId);
        var queue = await cache.GetMusicQueue(guildId);
        var settings = await cache.GetMusicPlayerSettings(guildId);

        return new
        {
            CurrentTrack = currentTrack,
            Queue = queue,
            player.State,
            player.Volume,
            player.Position,
            RepeatMode = settings.PlayerRepeat,
            Filters = new
            {
                BassBoost = player.Filters.Equalizer != null,
                Nightcore = player.Filters.Timescale?.Speed > 1.0f,
                Vaporwave = player.Filters.Timescale?.Speed < 1.0f,
                Karaoke = player.Filters.Karaoke != null,
                Tremolo = player.Filters.Tremolo != null,
                Vibrato = player.Filters.Vibrato != null,
                Rotation = player.Filters.Rotation != null,
                Distortion = player.Filters.Distortion != null,
                ChannelMix = player.Filters.ChannelMix != null
            },
            IsInVoiceChannel = isInVoiceChannel
        };
    }

    /// <summary>
    ///     Plays or enqueues a track
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="request">The play request containing URL and requester information</param>
    /// <returns>The loaded track and its position in the queue</returns>
    [Authorize("ApiKeyPolicy")]
    [HttpPost("play")]
    public async Task<IActionResult> Play(ulong guildId, [FromBody] PlayRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        var searchMode = request.Url.Contains("spotify") ? TrackSearchMode.Spotify :
            request.Url.Contains("youtube") ? TrackSearchMode.YouTube : TrackSearchMode.None;

        var trackResult = await audioService.Tracks.LoadTrackAsync(request.Url, new TrackLoadOptions
        {
            SearchMode = searchMode
        });
        if (trackResult is null)
            return BadRequest("Failed to load track");

        var queue = await cache.GetMusicQueue(guildId);
        queue.Add(new MewdekoTrack(queue.Count + 1, trackResult, request.Requester));
        await cache.SetMusicQueue(guildId, queue);

        if (player.State != PlayerState.Playing)
        {
            await player.PlayAsync(trackResult);
            await cache.SetCurrentTrack(guildId, queue[0]);

            // Notify clients of track change
            await eventManager.BroadcastPlayerUpdate(guildId);
        }

        return Ok(new
        {
            Track = trackResult, Position = queue.Count
        });
    }

    /// <summary>
    ///     Pauses or resumes playback
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>The new player state</returns>
    [HttpPost("pause")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> PauseResume(ulong guildId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        if (player.State == PlayerState.Playing)
            await player.PauseAsync();
        else
            await player.ResumeAsync();

        // Notify clients of state change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            player.State
        });
    }

    /// <summary>
    ///     Gets the current queue
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>The current music queue</returns>
    [HttpGet("queue")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetQueue(ulong guildId)
    {
        var queue = await cache.GetMusicQueue(guildId);
        return Ok(queue);
    }

    /// <summary>
    ///     Clears the queue
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>OK result when queue is cleared</returns>
    [HttpDelete("queue")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ClearQueue(ulong guildId)
    {
        await cache.SetMusicQueue(guildId, []);

        // Notify clients of queue change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Removes a track from the queue
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="index">The index of the track to remove</param>
    /// <returns>OK result when track is removed</returns>
    [HttpDelete("queue/{index}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> RemoveTrack(ulong guildId, int index)
    {
        var queue = await cache.GetMusicQueue(guildId);
        var track = queue.FirstOrDefault(x => x.Index == index);
        if (track == null)
            return NotFound("Track not found");

        queue.Remove(track);
        await cache.SetMusicQueue(guildId, queue);

        // Notify clients of queue change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Sets the volume
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="volume">The volume level (0-100)</param>
    /// <returns>The new volume level</returns>
    [HttpPost("volume/{volume}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SetVolume(ulong guildId, int volume)
    {
        if (volume is < 0 or > 100)
            return BadRequest("Volume must be between 0 and 100");

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        await player.SetVolumeAsync(volume / 100f);
        await player.SetGuildVolumeAsync(volume);

        // Notify clients of volume change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            Volume = volume
        });
    }

    /// <summary>
    ///     Seek to position in track
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="request">The seek request containing position in seconds</param>
    /// <returns>OK result when seek is successful</returns>
    [HttpPost("seek")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> Seek(ulong guildId, [FromBody] SeekRequest request)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        await player.SeekAsync(TimeSpan.FromSeconds(request.Position));

        // Notify clients of position change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Skips to the next track
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>OK result when skip is successful</returns>
    [HttpPost("skip")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SkipTrack(ulong guildId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        await player.SeekAsync(player.CurrentTrack.Duration);

        // Notify clients of track change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Goes to previous track
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>OK result when previous operation is successful</returns>
    [HttpPost("previous")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> PreviousTrack(ulong guildId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        // Implement previous track logic
        // This depends on your implementation of player.PreviousAsync() or similar

        // Notify clients of track change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Shuffles the queue
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>OK result when shuffle is successful</returns>
    [HttpPost("shuffle")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ShuffleQueue(ulong guildId)
    {
        var queue = await cache.GetMusicQueue(guildId);
        if (queue.Count <= 1)
            return Ok(); // Nothing to shuffle

        // Get the current track (if any)
        var currentTrack = queue.FirstOrDefault();

        // Remove current track from the shuffle
        if (currentTrack != null)
        {
            queue.Remove(currentTrack);
        }

        // Shuffle the remaining tracks
        var random = new Random();
        var shuffled = queue.OrderBy(x => random.Next()).ToList();

        // Reset indices
        for (var i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Index = i + 1;
        }

        // Re-add current track if it existed
        if (currentTrack != null)
        {
            currentTrack.Index = 0;
            shuffled.Insert(0, currentTrack);
        }

        await cache.SetMusicQueue(guildId, shuffled);

        // Notify clients of queue change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Sets the repeat mode
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="mode">The repeat mode (0=Off, 1=Track, 2=Queue)</param>
    /// <returns>OK result when repeat mode is set</returns>
    [HttpPost("repeat/{mode}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SetRepeatMode(ulong guildId, string mode)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);

        switch (mode.ToLower())
        {
            case "off":
            case "0":
                settings.PlayerRepeat = 0;
                break;
            case "track":
            case "1":
                settings.PlayerRepeat = (PlayerRepeatType)1;
                break;
            case "queue":
            case "2":
                settings.PlayerRepeat = (PlayerRepeatType)2;
                break;
            default:
                return BadRequest("Invalid repeat mode. Valid modes: off, track, queue");
        }

        await cache.SetMusicPlayerSettings(guildId, settings);

        // Notify clients of settings change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            RepeatMode = settings.PlayerRepeat
        });
    }

    /// <summary>
    ///     Gets or sets player settings
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>The current music player settings</returns>
    [HttpGet("settings")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);
        return Ok(settings);
    }

    /// <summary>
    ///     Updates player settings
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="settings">The new settings to apply</param>
    /// <returns>The updated settings</returns>
    [HttpPost("settings")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] MusicPlayerSettings settings)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        await player.SetMusicSettings(guildId, settings);

        // Notify clients of settings change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(settings);
    }

    /// <summary>
    ///     Gets or sets filters
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="filterName">The name of the filter to toggle</param>
    /// <param name="enable">Whether to enable or disable the filter</param>
    /// <returns>The filter status</returns>
    [HttpPost("filter/{filterName}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ToggleFilter(ulong guildId, string filterName, [FromBody] bool enable)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        switch (filterName.ToLower())
        {
            case "bass":
                player.Filters.Equalizer = enable
                    ? new EqualizerFilterOptions(new Equalizer
                    {
                        [0] = 0.6f, [1] = 0.67f, [2] = 0.67f, [3] = 0.4f
                    })
                    : null;
                break;
            case "nightcore":
                player.Filters.Timescale = enable
                    ? new TimescaleFilterOptions
                    {
                        Speed = 1.2f, Pitch = 1.2f, Rate = 1.0f
                    }
                    : null;
                break;
            case "vaporwave":
                player.Filters.Timescale = enable
                    ? new TimescaleFilterOptions
                    {
                        Speed = 0.8f, Pitch = 0.8f, Rate = 1.0f
                    }
                    : null;
                break;
            case "karaoke":
                player.Filters.Karaoke = enable
                    ? new KaraokeFilterOptions
                    {
                        Level = 1.0f, MonoLevel = 1.0f, FilterBand = 220.0f, FilterWidth = 100.0f
                    }
                    : null;
                break;
            case "tremolo":
                player.Filters.Tremolo = enable
                    ? new TremoloFilterOptions
                    {
                        Depth = 0.5f, Frequency = 2.0f
                    }
                    : null;
                break;
            case "vibrato":
                player.Filters.Vibrato = enable
                    ? new VibratoFilterOptions
                    {
                        Depth = 0.5f, Frequency = 2.0f
                    }
                    : null;
                break;
            case "rotation":
                player.Filters.Rotation = enable
                    ? new RotationFilterOptions
                    {
                        Frequency = 0.2f
                    }
                    : null;
                break;
            case "distortion":
                player.Filters.Distortion = enable
                    ? new DistortionFilterOptions
                    {
                        SinOffset = 0.0f,
                        SinScale = 1.0f,
                        CosOffset = 0.0f,
                        CosScale = 1.0f,
                        TanOffset = 0.0f,
                        TanScale = 1.0f,
                        Offset = 0.0f,
                        Scale = 5.0f
                    }
                    : null;
                break;
            case "channelmix":
                player.Filters.ChannelMix = enable
                    ? new ChannelMixFilterOptions
                    {
                        LeftToLeft = 1.0f, LeftToRight = 0.5f, RightToLeft = 0.5f, RightToRight = 1.0f
                    }
                    : null;
                break;
            default:
                return BadRequest("Unknown filter");
        }

        await player.Filters.CommitAsync();

        // Notify clients of filter change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            Filter = filterName, Enabled = enable
        });
    }

    /// <summary>
    ///     A song request
    /// </summary>
    public class PlayRequest
    {
        /// <summary>
        ///     The requested url
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        ///     Who requested
        /// </summary>
        public PartialUser Requester { get; set; }
    }

    /// <summary>
    ///     Seek request
    /// </summary>
    public class SeekRequest
    {
        /// <summary>
        ///     Position in seconds
        /// </summary>
        public double Position { get; set; }
    }
}