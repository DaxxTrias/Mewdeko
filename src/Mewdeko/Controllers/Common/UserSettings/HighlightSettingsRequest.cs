namespace Mewdeko.Controllers.Common.UserSettings;

/// <summary>
///     Request model for updating highlight settings
/// </summary>
public class HighlightSettingsRequest
{
    /// <summary>
    ///     Whether highlights are enabled
    /// </summary>
    public bool HighlightsEnabled { get; set; }
}