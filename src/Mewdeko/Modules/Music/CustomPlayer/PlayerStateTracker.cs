using System.Threading;
using Mewdeko.Modules.Music.Common;
using Serilog;

namespace Mewdeko.Modules.Music.CustomPlayer
{
    /// <summary>
    ///     Tracks and periodically saves the state of a music player to enable recovery after bot restarts.
    /// </summary>
    public class PlayerStateTracker : IDisposable
    {
        private readonly MewdekoPlayer player;
        private readonly IDataCache cache;
        private readonly Timer updateTimer;
        private const int UpdateIntervalMs = 1000;
        private bool disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PlayerStateTracker"/> class.
        /// </summary>
        /// <param name="player">The player to track.</param>
        /// <param name="cache">The data cache for persistence.</param>
        public PlayerStateTracker(MewdekoPlayer player, IDataCache cache)
        {
            this.player = player;
            this.cache = cache;

            // Start periodic updates
            updateTimer = new Timer(UpdateState, null, UpdateIntervalMs, UpdateIntervalMs);
        }

        /// <summary>
        ///     Updates the player state in Redis.
        /// </summary>
        /// <param name="state">The timer state (unused).</param>
        private async void UpdateState(object state)
        {
            try
            {
                // Only track if player is actively playing or paused
                if (player.State != Lavalink4NET.Players.PlayerState.Playing &&
                    player.State != Lavalink4NET.Players.PlayerState.Paused)
                    return;

                if (player.CurrentItem == null)
                    return;

                var playerState = new MusicPlayerState
                {
                    GuildId = player.GuildId,
                    VoiceChannelId = player.VoiceChannelId,
                    CurrentPosition = player.Position?.Position ?? TimeSpan.Zero,
                    IsPlaying = player.State == Lavalink4NET.Players.PlayerState.Playing,
                    IsPaused = player.State == Lavalink4NET.Players.PlayerState.Paused,
                    Volume = player.Volume,
                    LastUpdateTime = DateTime.UtcNow,
                    RepeatMode = await player.GetRepeatType(),
                    AutoPlayAmount = await player.GetAutoPlay()
                };

                await cache.SetPlayerState(player.GuildId, playerState);
                Log.Verbose("Updated player state for guild {GuildId}, position: {Position}",
                    player.GuildId, playerState.CurrentPosition);
            }
            catch (Exception ex)
            {
                // Log but don't throw to avoid crashing the timer
                Log.Error(ex, "Failed to update player state for guild {GuildId}", player.GuildId);
            }
        }

        /// <summary>
        ///     Forces an immediate state update.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ForceUpdate()
        {
            try
            {
                if (player.State != Lavalink4NET.Players.PlayerState.Playing &&
                    player.State != Lavalink4NET.Players.PlayerState.Paused)
                    return;

                if (player.CurrentItem == null)
                    return;

                var playerState = new MusicPlayerState
                {
                    GuildId = player.GuildId,
                    VoiceChannelId = player.VoiceChannelId,
                    CurrentPosition = player.Position?.Position ?? TimeSpan.Zero,
                    IsPlaying = player.State == Lavalink4NET.Players.PlayerState.Playing,
                    IsPaused = player.State == Lavalink4NET.Players.PlayerState.Paused,
                    Volume = player.Volume,
                    LastUpdateTime = DateTime.UtcNow,
                    RepeatMode = await player.GetRepeatType(),
                    AutoPlayAmount = await player.GetAutoPlay()
                };

                await cache.SetPlayerState(player.GuildId, playerState);
                Log.Debug("Forced player state update for guild {GuildId}, position: {Position}",
                    player.GuildId, playerState.CurrentPosition);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to force update player state for guild {GuildId}", player.GuildId);
            }
        }

        /// <summary>
        ///     Disposes the state tracker and stops updates.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            updateTimer?.Dispose();
            disposed = true;
        }
    }
}