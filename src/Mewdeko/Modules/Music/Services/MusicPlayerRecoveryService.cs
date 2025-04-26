using System.Threading;
using Lavalink4NET;
using Lavalink4NET.Players;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Music.CustomPlayer;
using Microsoft.Extensions.Options;
using Serilog;

namespace Mewdeko.Modules.Music.Services;

/// <summary>
///     Service that recovers music players after bot restarts.
/// </summary>
public class MusicPlayerRecoveryService : INService, IReadyExecutor
{
    private readonly IDataCache cache;
    private readonly IAudioService audioService;
    private readonly DiscordShardedClient client;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MusicPlayerRecoveryService" /> class.
    /// </summary>
    /// <param name="cache">The data cache service.</param>
    /// <param name="audioService">The audio service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="services">The service provider.</param>
    public MusicPlayerRecoveryService(
        IDataCache cache,
        IAudioService audioService,
        DiscordShardedClient client,
        IServiceProvider services)
    {
        this.cache = cache;
        this.audioService = audioService;
        this.client = client;
    }

    /// <summary>
    ///     Executes when the bot is ready, recovering any active music players.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        try
        {
            Log.Information("Starting music player recovery process");
            await RecoverPlayersAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute player recovery");
        }
    }

    /// <summary>
    ///     Recovers all active music players across all guilds.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RecoverPlayersAsync()
    {
        // Process guilds in batches to avoid rate limiting
        var guilds = client.Guilds.ToList();
        Log.Information("Checking {Count} guilds for music player recovery", guilds.Count);

        // Process up to 5 guilds concurrently
        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(5);

        foreach (var guild in guilds)
        {
            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await RecoverGuildPlayerAsync(guild);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        Log.Information("Music player recovery completed");
    }

    /// <summary>
    ///     Recovers a music player for a specific guild.
    /// </summary>
    /// <param name="guild">The guild to recover player for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RecoverGuildPlayerAsync(SocketGuild guild)
    {
        try
        {
            // Check if we have a saved state for this guild
            var state = await cache.GetPlayerState(guild.Id);
            if (state == null)
            {
                Log.Debug("No saved player state found for guild {GuildId}", guild.Id);
                return;
            }

            // Check if state is too old (more than 15 minutes)
            if (DateTime.UtcNow - state.LastUpdateTime > TimeSpan.FromMinutes(15))
            {
                Log.Warning("Skipping recovery for guild {GuildId} - state too old ({LastUpdate})",
                    guild.Id, state.LastUpdateTime);
                await cache.RemovePlayerState(guild.Id);
                return;
            }

            // Check if voice channel still exists
            var voiceChannel = guild.GetVoiceChannel(state.VoiceChannelId);
            if (voiceChannel == null)
            {
                Log.Warning("Skipping recovery for guild {GuildId} - voice channel {ChannelId} no longer exists",
                    guild.Id, state.VoiceChannelId);
                await cache.RemovePlayerState(guild.Id);
                return;
            }

            // Get the queue and current track
            var queue = await cache.GetMusicQueue(guild.Id);
            var currentTrack = await cache.GetCurrentTrack(guild.Id);

            if (currentTrack == null || queue.Count == 0)
            {
                Log.Warning("Skipping recovery for guild {GuildId} - no current track or empty queue", guild.Id);
                await cache.RemovePlayerState(guild.Id);
                return;
            }

            Log.Information("Recovering player for guild {GuildId}, track: {TrackTitle}, position: {Position}",
                guild.Id, currentTrack.Track.Title, state.CurrentPosition);

            // Get the music channel
            IMessageChannel messageChannel = null;
            try
            {
                var settings = await cache.GetMusicPlayerSettings(guild.Id);
                if (settings?.MusicChannelId != null)
                    messageChannel = guild.GetTextChannel(settings.MusicChannelId.Value);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not get configured music channel for guild {GuildId}", guild.Id);
            }

            // Fallback to first text channel if needed
            if (messageChannel == null)
            {
                messageChannel = guild.TextChannels
                    .FirstOrDefault(c => c.Users.Any(u => u.Id == client.CurrentUser.Id));

                if (messageChannel == null)
                    messageChannel = guild.DefaultChannel ?? guild.TextChannels.FirstOrDefault();

                if (messageChannel == null)
                {
                    Log.Warning("Skipping recovery for guild {GuildId} - cannot find valid text channel", guild.Id);
                    return;
                }
            }


            var retrieveOptions = new PlayerRetrieveOptions(PlayerChannelBehavior.Join);

            var player = await audioService.Players
                .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(guild.Id, voiceChannel.Id, CreatePlayerAsync,
                    new OptionsWrapper<MewdekoPlayerOptions>(new MewdekoPlayerOptions
                    {
                        Channel = voiceChannel
                    }),
                    retrieveOptions)
                .ConfigureAwait(false);

            // Restore volume
            await player.Player.SetVolumeAsync(state.Volume);
            await player.Player.SetGuildVolumeAsync((int)(state.Volume * 100));

            // Set repeat type and autoplay
            await player.Player.SetRepeatTypeAsync(state.RepeatMode);
            await player.Player.SetAutoPlay(state.AutoPlayAmount);

            // Play track
            await player.Player.PlayAsync(currentTrack.Track);


            await player.Player.SeekAsync(state.CurrentPosition);
            Log.Debug("Seeking to position {Position} for guild {GuildId}", state.CurrentPosition, guild.Id);

            // Restore pause state if needed
            if (state.IsPaused)
            {
                await player.Player.PauseAsync();
                Log.Debug("Restoring paused state for guild {GuildId}", guild.Id);
            }

            // Send notification to channel
            try
            {
                var resumeEmbed = new EmbedBuilder()
                    .WithTitle("🎵 Music Playback Resumed")
                    .WithDescription($"Reconnected to voice channel and resumed playback at position " +
                                     $"{state.CurrentPosition:hh\\:mm\\:ss} after bot restart.")
                    .WithColor(new Color(30, 215, 96))
                    .WithFooter($"Track: {currentTrack.Track.Title}")
                    .Build();

                await messageChannel.SendMessageAsync(embed: resumeEmbed);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not send recovery notification to channel in guild {GuildId}", guild.Id);
            }

            // Clean up state as we've recovered it
            await cache.RemovePlayerState(guild.Id);
            Log.Information("Successfully recovered player for guild {GuildId}", guild.Id);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to recover player for guild {GuildId}:{ex}", guild.Id, ex);

            // Try to clean up the failed state
            try
            {
                await cache.RemovePlayerState(guild.Id);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static ValueTask<MewdekoPlayer> CreatePlayerAsync(
        IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new MewdekoPlayer(properties));
    }
}