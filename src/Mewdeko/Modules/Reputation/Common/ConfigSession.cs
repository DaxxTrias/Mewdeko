using DataModel;

namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Represents an active configuration session.
/// </summary>
public class ConfigSession
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID who started the session.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where the session is active.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the message ID of the configuration interface.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     Gets or sets the current configuration.
    /// </summary>
    public RepConfig Config { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the current configuration category.
    /// </summary>
    public ConfigCategory CurrentCategory { get; set; }
}