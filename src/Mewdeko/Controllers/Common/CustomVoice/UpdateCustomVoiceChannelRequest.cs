namespace Mewdeko.Controllers.Common.CustomVoice;

/// <summary>
///     Request model for updating custom voice channel settings
/// </summary>
public class UpdateCustomVoiceChannelRequest
{
    /// <summary>
    ///     Whether the channel should be locked (only allowed users can join)
    /// </summary>
    public bool? IsLocked { get; set; }

    /// <summary>
    ///     Whether the channel should be kept alive even when empty
    /// </summary>
    public bool? KeepAlive { get; set; }

    /// <summary>
    ///     List of user IDs explicitly allowed to join the channel
    /// </summary>
    public List<ulong>? AllowedUsers { get; set; }

    /// <summary>
    ///     List of user IDs explicitly denied from joining the channel
    /// </summary>
    public List<ulong>? DeniedUsers { get; set; }
}