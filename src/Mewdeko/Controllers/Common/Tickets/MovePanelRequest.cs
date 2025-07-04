namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for moving a panel
/// </summary>
public class MovePanelRequest
{
    /// <summary>
    ///     The target channel ID
    /// </summary>
    public ulong ChannelId { get; set; }
}