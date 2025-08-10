namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Configuration categories for the interactive interface.
/// </summary>
public enum ConfigCategory
{
    /// <summary>
    ///     Basic settings (enable/disable, anonymous, negative rep).
    /// </summary>
    Basic,

    /// <summary>
    ///     Cooldowns and limits configuration.
    /// </summary>
    Cooldowns,

    /// <summary>
    ///     User requirements to give reputation.
    /// </summary>
    Requirements,

    /// <summary>
    ///     Notification settings.
    /// </summary>
    Notifications,

    /// <summary>
    ///     Advanced features like decay.
    /// </summary>
    Advanced
}