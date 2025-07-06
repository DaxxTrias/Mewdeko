using Color = Discord.Color;

namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for creating a new panel
/// </summary>
public class CreatePanelRequest
{
    /// <summary>
    ///     The ID of the channel to create the panel in
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Optional custom embed JSON configuration
    /// </summary>
    public string? EmbedJson { get; set; }

    /// <summary>
    ///     Default title if not using custom JSON
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     Default description if not using custom JSON
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Default color if not using custom JSON
    /// </summary>
    public Color? Color { get; set; }
}