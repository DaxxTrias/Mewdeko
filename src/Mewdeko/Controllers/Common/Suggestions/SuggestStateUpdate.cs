using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Controllers.Common.Suggestions;

/// <summary>
/// </summary>
public class SuggestStateUpdate
{
    /// <summary>
    /// </summary>
    public SuggestionsService.SuggestState State { get; set; }

    /// <summary>
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// </summary>
    public ulong UserId { get; set; }
}