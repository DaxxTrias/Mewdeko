namespace Mewdeko.Controllers.Common.Filter;

/// <summary>
///     Request model for server-wide filter settings
/// </summary>
public class ServerFilterSettingsRequest
{
    /// <summary>
    ///     Whether to filter words server-wide
    /// </summary>
    public bool FilterWords { get; set; }

    /// <summary>
    ///     Whether to filter invites server-wide
    /// </summary>
    public bool FilterInvites { get; set; }

    /// <summary>
    ///     Whether to filter links server-wide
    /// </summary>
    public bool FilterLinks { get; set; }
}