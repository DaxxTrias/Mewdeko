using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-mass mention configuration
/// </summary>
public class AntiMassMentionConfigRequest
{
    /// <summary>
    ///     Whether anti-mass mention protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The number of mentions that triggers the protection
    /// </summary>
    public int MentionThreshold { get; set; }

    /// <summary>
    ///     The time window in seconds for counting mentions
    /// </summary>
    public int TimeWindowSeconds { get; set; }

    /// <summary>
    ///     The maximum allowed mentions within the time window
    /// </summary>
    public int MaxMentionsInTimeWindow { get; set; }

    /// <summary>
    ///     Whether to ignore bot mentions
    /// </summary>
    public bool IgnoreBots { get; set; }

    /// <summary>
    ///     The punishment action to be applied when the protection is triggered
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the mute in minutes, if applicable
    /// </summary>
    public int MuteTime { get; set; }

    /// <summary>
    ///     The ID of the role to be added as punishment, if applicable
    /// </summary>
    public ulong? RoleId { get; set; }
}