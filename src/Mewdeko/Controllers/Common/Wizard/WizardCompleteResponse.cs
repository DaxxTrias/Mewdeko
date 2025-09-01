namespace Mewdeko.Controllers.Common.Wizard;

/// <summary>
///     Response model when completing the wizard setup
/// </summary>
public class WizardCompleteResponse
{
    /// <summary>
    ///     Whether the completion was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Guild ID where wizard was completed
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     User ID who completed the wizard
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Features that were successfully configured
    /// </summary>
    public string[] ConfiguredFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Features that failed to configure
    /// </summary>
    public FeatureConfigResult[] FailedFeatures { get; set; } = Array.Empty<FeatureConfigResult>();

    /// <summary>
    ///     When the wizard was completed
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    ///     User's new experience level after completion
    /// </summary>
    public int NewExperienceLevel { get; set; }

    /// <summary>
    ///     Whether this was the user's first wizard completion
    /// </summary>
    public bool WasFirstWizard { get; set; }

    /// <summary>
    ///     Next steps or recommendations for the user
    /// </summary>
    public string[] NextSteps { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Error message if completion failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
///     Result of configuring an individual feature
/// </summary>
public class FeatureConfigResult
{
    /// <summary>
    ///     Feature identifier
    /// </summary>
    public string FeatureId { get; set; } = "";

    /// <summary>
    ///     Human-readable feature name
    /// </summary>
    public string FeatureName { get; set; } = "";

    /// <summary>
    ///     Whether the feature was configured successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Error message if configuration failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     Configuration details that were applied
    /// </summary>
    public Dictionary<string, object> ConfigurationApplied { get; set; } = new();
}

/// <summary>
///     Available wizard features that can be configured
/// </summary>
public static class WizardFeatures
{
    /// <summary>
    ///     Multi-greeting system
    /// </summary>
    public const string MultiGreets = "multigreets";

    /// <summary>
    ///     Moderation features
    /// </summary>
    public const string Moderation = "moderation";

    /// <summary>
    ///     XP and leveling system
    /// </summary>
    public const string XpSystem = "xp";

    /// <summary>
    ///     Starboard feature
    /// </summary>
    public const string Starboard = "starboard";

    /// <summary>
    ///     Logging configuration
    /// </summary>
    public const string Logging = "logging";

    /// <summary>
    ///     Auto-assign roles
    /// </summary>
    public const string AutoRoles = "autoroles";

    /// <summary>
    ///     Protection systems
    /// </summary>
    public const string Protection = "protection";

    /// <summary>
    ///     Music features
    /// </summary>
    public const string Music = "music";
}

/// <summary>
///     Wizard step identifiers
/// </summary>
public static class WizardSteps
{
    /// <summary>
    ///     Welcome step
    /// </summary>
    public const int Welcome = 1;

    /// <summary>
    ///     Permission check step
    /// </summary>
    public const int PermissionCheck = 2;

    /// <summary>
    ///     Feature selection step
    /// </summary>
    public const int FeatureSelection = 3;

    /// <summary>
    ///     Feature configuration step
    /// </summary>
    public const int FeatureConfiguration = 4;

    /// <summary>
    ///     Completion step
    /// </summary>
    public const int Completion = 5;
}