namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Defines the type of XP level-up notification.
/// </summary>
public enum XpNotificationType
{
    /// <summary>
    ///     No notification.
    /// </summary>
    None,

    /// <summary>
    ///     Send notification to the channel where XP was earned.
    /// </summary>
    Channel,

    /// <summary>
    ///     Send notification as a direct message to the user.
    /// </summary>
    Dm
}

/// <summary>
///     Represents an XP level-up notification to be sent.
/// </summary>
public class XpNotification
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
    ///     Gets or sets the new level reached.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where the XP was earned.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the notification type preference.
    /// </summary>
    public XpNotificationType NotificationType { get; set; }

    /// <summary>
    ///     Gets or sets the sources of XP that contributed to this level-up.
    /// </summary>
    public string Sources { get; set; }
}