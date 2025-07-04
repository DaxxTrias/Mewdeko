namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for setting channel configurations
/// </summary>
public class SetChannelRequest
{
    /// <summary>
    ///     The channel ID to set (0 to remove/disable)
    /// </summary>
    public ulong ChannelId { get; set; }
}