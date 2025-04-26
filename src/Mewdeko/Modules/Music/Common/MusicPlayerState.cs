namespace Mewdeko.Modules.Music.Common
{
    /// <summary>
    ///     Represents the state of a music player that can be persisted and restored.
    /// </summary>
    public class MusicPlayerState
    {
        /// <summary>
        ///     The ID of the guild this player state belongs to.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        ///     The ID of the voice channel the player was connected to.
        /// </summary>
        public ulong VoiceChannelId { get; set; }

        /// <summary>
        ///     The current playback position in the track when state was saved.
        /// </summary>
        public TimeSpan CurrentPosition { get; set; }

        /// <summary>
        ///     Indicates whether the player was actively playing.
        /// </summary>
        public bool IsPlaying { get; set; }

        /// <summary>
        ///     Indicates whether the player was paused.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        ///     The volume level of the player.
        /// </summary>
        public float Volume { get; set; }

        /// <summary>
        ///     The UTC timestamp when this state was last updated.
        /// </summary>
        public DateTime LastUpdateTime { get; set; }

        /// <summary>
        ///     The repeat mode that was active for this player.
        /// </summary>
        public PlayerRepeatType RepeatMode { get; set; }

        /// <summary>
        ///     The number of songs to autoplay after queue ends.
        /// </summary>
        public int AutoPlayAmount { get; set; }
    }
}