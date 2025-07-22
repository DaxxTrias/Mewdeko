namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for duplicating a panel
/// </summary>
public class DuplicatePanelRequest
{
    /// <summary>
    ///     The target channel ID for the duplicate
    /// </summary>
    public ulong ChannelId { get; set; }
}