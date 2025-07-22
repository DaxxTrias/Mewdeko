namespace Mewdeko.Controllers.Common.Starboard;

/// <summary>
///     Request model for creating a new starboard
/// </summary>
public class StarboardCreateRequest
{
    /// <summary>
    ///     The channel ID where starred messages will be posted
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The emote to use for this starboard
    /// </summary>
    public string Emote { get; set; } = "⭐";

    /// <summary>
    ///     The number of reactions required to post a message
    /// </summary>
    public int Threshold { get; set; } = 1;
}