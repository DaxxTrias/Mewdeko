namespace Mewdeko.Controllers.Common.Logging;

/// <summary>
///     Request model for setting ignored channels
/// </summary>
public class SetIgnoredChannelsRequest
{
    /// <summary>
    ///     List of channel IDs to ignore
    /// </summary>
    public List<ulong> ChannelIds { get; set; } = new();
}