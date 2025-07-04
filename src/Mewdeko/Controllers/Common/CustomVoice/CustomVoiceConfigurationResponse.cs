namespace Mewdeko.Controllers.Common.CustomVoice;

/// <summary>
///     Response model for custom voice configuration
/// </summary>
public class CustomVoiceConfigurationResponse
{
    /// <summary>
    ///     Whether custom voice is enabled for the guild
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The ID of the hub voice channel that users join to create custom channels
    /// </summary>
    public ulong HubVoiceChannelId { get; set; }

    /// <summary>
    ///     The category ID where custom voice channels will be created
    /// </summary>
    public ulong? ChannelCategoryId { get; set; }

    /// <summary>
    ///     Default name format for new custom voice channels
    /// </summary>
    public string DefaultNameFormat { get; set; } = string.Empty;

    /// <summary>
    ///     Default user limit for new custom voice channels
    /// </summary>
    public int DefaultUserLimit { get; set; }

    /// <summary>
    ///     Default bitrate for new custom voice channels
    /// </summary>
    public int DefaultBitrate { get; set; }

    /// <summary>
    ///     Whether to delete custom voice channels when they become empty
    /// </summary>
    public bool DeleteWhenEmpty { get; set; }

    /// <summary>
    ///     Timeout in minutes before empty channels are deleted
    /// </summary>
    public int EmptyChannelTimeout { get; set; }

    /// <summary>
    ///     Whether users can create multiple custom voice channels
    /// </summary>
    public bool AllowMultipleChannels { get; set; }

    /// <summary>
    ///     Whether users can customize their channel names
    /// </summary>
    public bool AllowNameCustomization { get; set; }

    /// <summary>
    ///     Whether users can customize their channel user limits
    /// </summary>
    public bool AllowUserLimitCustomization { get; set; }

    /// <summary>
    ///     Whether users can customize their channel bitrates
    /// </summary>
    public bool AllowBitrateCustomization { get; set; }

    /// <summary>
    ///     Whether users can lock their custom voice channels
    /// </summary>
    public bool AllowLocking { get; set; }

    /// <summary>
    ///     Whether users can manage allowed/denied users for their channels
    /// </summary>
    public bool AllowUserManagement { get; set; }

    /// <summary>
    ///     Maximum user limit that can be set for custom voice channels
    /// </summary>
    public int MaxUserLimit { get; set; }

    /// <summary>
    ///     Maximum bitrate that can be set for custom voice channels
    /// </summary>
    public int MaxBitrate { get; set; }

    /// <summary>
    ///     Whether to save user preferences for future channel creation
    /// </summary>
    public bool PersistUserPreferences { get; set; }

    /// <summary>
    ///     Whether to automatically grant permissions to channel owners
    /// </summary>
    public bool AutoPermission { get; set; }

    /// <summary>
    ///     Role ID for custom voice administrators (can manage all custom channels)
    /// </summary>
    public ulong? CustomVoiceAdminRoleId { get; set; }
}