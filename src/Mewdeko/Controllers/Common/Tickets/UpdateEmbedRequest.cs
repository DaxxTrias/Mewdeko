namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for updating panel embed
/// </summary>
public class UpdateEmbedRequest
{
    /// <summary>
    ///     The new embed JSON configuration
    /// </summary>
    public string EmbedJson { get; set; } = null!;
}