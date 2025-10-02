namespace Mewdeko.Controllers.Common.Wizard;

/// <summary>
///     Response model for wizard decision API calls
/// </summary>
public class WizardDecisionResponse
{
    /// <summary>
    ///     Whether the wizard should be shown
    /// </summary>
    public bool ShowWizard { get; set; }

    /// <summary>
    ///     Whether to show a setup suggestion banner instead
    /// </summary>
    public bool ShowSuggestion { get; set; }

    /// <summary>
    ///     Type of wizard to show if ShowWizard is true
    /// </summary>
    public WizardType WizardType { get; set; }

    /// <summary>
    ///     Reason for the decision (for debugging/logging)
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    ///     Additional context for the wizard
    /// </summary>
    public WizardContext Context { get; set; } = new();
}

/// <summary>
///     Additional context information for the wizard
/// </summary>
public class WizardContext
{
    /// <summary>
    ///     User's dashboard experience level
    /// </summary>
    public int ExperienceLevel { get; set; }

    /// <summary>
    ///     Whether this is the user's first dashboard access
    /// </summary>
    public bool IsFirstDashboardAccess { get; set; }

    /// <summary>
    ///     Number of guilds where user has completed the wizard
    /// </summary>
    public int CompletedWizardCount { get; set; }

    /// <summary>
    ///     Whether the guild has any basic setup
    /// </summary>
    public bool GuildHasBasicSetup { get; set; }
}