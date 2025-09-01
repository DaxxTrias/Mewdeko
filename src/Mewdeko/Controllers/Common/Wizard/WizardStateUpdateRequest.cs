namespace Mewdeko.Controllers.Common.Wizard;

/// <summary>
///     Request model for updating wizard state
/// </summary>
public class WizardStateUpdateRequest
{
    /// <summary>
    ///     Current step of the wizard
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    ///     Features that have been configured so far
    /// </summary>
    public string[] ConfiguredFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Whether this update marks the wizard as completed
    /// </summary>
    public bool MarkCompleted { get; set; }

    /// <summary>
    ///     Whether this update marks the wizard as skipped
    /// </summary>
    public bool MarkSkipped { get; set; }

    /// <summary>
    ///     User ID making the update
    /// </summary>
    public ulong UserId { get; set; }
}