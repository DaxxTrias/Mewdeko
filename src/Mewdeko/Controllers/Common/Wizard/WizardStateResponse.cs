namespace Mewdeko.Controllers.Common.Wizard;

/// <summary>
///     Response model for guild wizard state
/// </summary>
public class WizardStateResponse
{
    /// <summary>
    ///     Guild ID
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether the wizard has been completed
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    ///     Whether the wizard was skipped
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>
    ///     When the wizard was completed/skipped
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     User ID who completed/skipped the wizard
    /// </summary>
    public ulong? CompletedByUserId { get; set; }

    /// <summary>
    ///     Whether the guild has basic setup configured
    /// </summary>
    public bool HasBasicSetup { get; set; }

    /// <summary>
    ///     Current wizard step (if in progress)
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    ///     Features that have been configured
    /// </summary>
    public string[] ConfiguredFeatures { get; set; } = Array.Empty<string>();
}