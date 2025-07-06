namespace Mewdeko.Controllers.Common.Filter;

/// <summary>
///     Request model for warning settings
/// </summary>
public class WarningSettingsRequest
{
    /// <summary>
    ///     Whether to warn on filtered words
    /// </summary>
    public bool? WarnOnFilteredWord { get; set; }

    /// <summary>
    ///     Whether to warn on invites
    /// </summary>
    public bool? WarnOnInvite { get; set; }
}