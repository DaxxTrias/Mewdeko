namespace Mewdeko.Controllers.Common.Logging;

/// <summary>
///     Request model for setting a log channel
/// </summary>
public class SetLogChannelRequest
{
    /// <summary>
    ///     The channel ID to set for logging (null to disable)
    /// </summary>
    public ulong? ChannelId { get; set; }
}