namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Represents an XP gain item to be processed.
/// </summary>
public class XpGainItem
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the amount of XP to add.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where the XP was earned.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the source of the XP gain.
    /// </summary>
    public XpSource Source { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp of the XP gain.
    /// </summary>
    public DateTime Timestamp { get; set; }
}