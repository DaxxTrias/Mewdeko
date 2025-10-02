namespace Mewdeko.Controllers.Common.UserSettings;

/// <summary>
///     Request model for updating global user preferences
/// </summary>
public class UserPreferencesRequest
{
    /// <summary>
    ///     Whether level-up pings are disabled across all guilds
    /// </summary>
    public bool? LevelUpPingsDisabled { get; set; }

    /// <summary>
    ///     Whether automatic pronoun fetching from PronounDB is disabled
    /// </summary>
    public bool? PronounsDisabled { get; set; }

    /// <summary>
    ///     Whether user prefers guided setup in the dashboard
    /// </summary>
    public bool? PrefersGuidedSetup { get; set; }

    /// <summary>
    ///     User's dashboard experience level (0=beginner, 1=intermediate, 2=advanced)
    /// </summary>
    public int? DashboardExperienceLevel { get; set; }
}