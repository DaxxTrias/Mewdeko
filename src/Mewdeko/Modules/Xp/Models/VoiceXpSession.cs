namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Represents an active voice channel XP tracking session.
/// </summary>
public class VoiceXpSession
{
    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the voice channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the session start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    ///     Gets or sets the last time XP was processed for this session.
    /// </summary>
    public DateTime LastProcessed { get; set; }

    /// <summary>
    ///     Gets or sets the unique key for this session.
    /// </summary>
    public string SessionKey { get; set; }
}