using Mewdeko.Modules.Music.Common;

namespace Mewdeko.Controllers.Common.Music;

/// <summary>
///     A song request
/// </summary>
public class PlayRequest
{
    /// <summary>
    ///     The requested url
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    ///     Who requested
    /// </summary>
    public PartialUser Requester { get; set; }
}