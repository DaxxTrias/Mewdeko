namespace Mewdeko.Controllers.Common.Wizard;

/// <summary>
///     Base request model for feature setup during wizard
/// </summary>
public class FeatureSetupRequest
{
    /// <summary>
    ///     Guild ID where the feature is being set up
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     User ID performing the setup
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Feature identifier
    /// </summary>
    public string FeatureId { get; set; } = "";
}

/// <summary>
///     Request for setting up welcome messages
/// </summary>
public class WelcomeSetupRequest : FeatureSetupRequest
{
    /// <summary>
    ///     Channel ID for welcome messages
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Welcome message text with placeholders
    /// </summary>
    public string WelcomeMessage { get; set; } = "";

    /// <summary>
    ///     Whether to send DM greetings
    /// </summary>
    public bool SendDmGreeting { get; set; }

    /// <summary>
    ///     DM greeting message text
    /// </summary>
    public string DmGreetingMessage { get; set; } = "";

    /// <summary>
    ///     Whether to auto-delete welcome messages
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    ///     Auto-delete timer in seconds
    /// </summary>
    public int AutoDeleteTimer { get; set; } = 30;
}

/// <summary>
///     Request for setting up moderation features
/// </summary>
public class ModerationSetupRequest : FeatureSetupRequest
{
    /// <summary>
    ///     Whether to filter invite links
    /// </summary>
    public bool FilterInvites { get; set; }

    /// <summary>
    ///     Whether to filter other links
    /// </summary>
    public bool FilterLinks { get; set; }

    /// <summary>
    ///     Whether to filter bad words
    /// </summary>
    public bool FilterWords { get; set; }

    /// <summary>
    ///     Custom filtered words list
    /// </summary>
    public string[] CustomFilteredWords { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Mute role ID for moderation actions
    /// </summary>
    public ulong? MuteRoleId { get; set; }

    /// <summary>
    ///     Moderation log channel ID
    /// </summary>
    public ulong? LogChannelId { get; set; }
}

/// <summary>
///     Request for setting up XP system
/// </summary>
public class XpSetupRequest : FeatureSetupRequest
{
    /// <summary>
    ///     XP gain rate for text messages
    /// </summary>
    public int TextXpRate { get; set; } = 3;

    /// <summary>
    ///     XP gain timeout in seconds
    /// </summary>
    public int XpTimeout { get; set; } = 60;

    /// <summary>
    ///     XP gain rate for voice activity
    /// </summary>
    public int VoiceXpRate { get; set; } = 2;

    /// <summary>
    ///     Voice XP timeout in seconds
    /// </summary>
    public int VoiceXpTimeout { get; set; } = 300;

    /// <summary>
    ///     Level-up notification channel
    /// </summary>
    public ulong? LevelUpChannelId { get; set; }

    /// <summary>
    ///     Role rewards for levels
    /// </summary>
    public XpRoleReward[] RoleRewards { get; set; } = Array.Empty<XpRoleReward>();
}

/// <summary>
///     XP role reward configuration
/// </summary>
public class XpRoleReward
{
    /// <summary>
    ///     Level required for this role
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Role ID to grant
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Whether to remove previous level roles
    /// </summary>
    public bool RemovePrevious { get; set; }
}

/// <summary>
///     Request for setting up starboard
/// </summary>
public class StarboardSetupRequest : FeatureSetupRequest
{
    /// <summary>
    ///     Starboard channel ID
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Number of reactions required to reach starboard
    /// </summary>
    public int Threshold { get; set; } = 3;

    /// <summary>
    ///     Emoji to use for starring (default ⭐)
    /// </summary>
    public string StarEmoji { get; set; } = "⭐";

    /// <summary>
    ///     Whether to allow bot messages on starboard
    /// </summary>
    public bool AllowBots { get; set; } = false;

    /// <summary>
    ///     Whether to remove starboard posts when original is deleted
    /// </summary>
    public bool RemoveOnDelete { get; set; } = true;
}