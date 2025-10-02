using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Modules.Counting.Common;

/// <summary>
///     Represents the effective moderation configuration for a counting channel (combining defaults and overrides).
/// </summary>
public class CountingModerationEffectiveConfig
{
    /// <summary>
    ///     The channel ID this configuration applies to.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Whether moderation is enabled for this channel.
    /// </summary>
    public bool EnableModeration { get; set; }

    /// <summary>
    ///     Threshold of wrong counts before punishment is applied.
    /// </summary>
    public int WrongCountThreshold { get; set; } = 3;

    /// <summary>
    ///     Time window in hours for tracking wrong counts.
    /// </summary>
    public int TimeWindowHours { get; set; } = 24;

    /// <summary>
    ///     Punishment action to apply.
    /// </summary>
    public PunishmentAction PunishmentAction { get; set; }

    /// <summary>
    ///     Duration for timed punishments in minutes (0 = permanent).
    /// </summary>
    public int PunishmentDurationMinutes { get; set; }

    /// <summary>
    ///     Role ID for AddRole punishment type.
    /// </summary>
    public ulong? PunishmentRoleId { get; set; }

    /// <summary>
    ///     List of role IDs to ignore from counting.
    /// </summary>
    public List<ulong> IgnoreRoles { get; set; } = new();

    /// <summary>
    ///     Whether to delete messages from ignored roles.
    /// </summary>
    public bool DeleteIgnoredMessages { get; set; }

    /// <summary>
    ///     List of role IDs required to count.
    /// </summary>
    public List<ulong> RequiredRoles { get; set; } = new();

    /// <summary>
    ///     List of role IDs banned from counting.
    /// </summary>
    public List<ulong> BannedRoles { get; set; } = new();

    /// <summary>
    ///     Whether to punish non-number messages.
    /// </summary>
    public bool PunishNonNumbers { get; set; }

    /// <summary>
    ///     Whether to delete non-number messages.
    /// </summary>
    public bool DeleteNonNumbers { get; set; } = true;

    /// <summary>
    ///     Whether to punish message edits.
    /// </summary>
    public bool PunishEdits { get; set; }

    /// <summary>
    ///     Whether to delete edited messages.
    /// </summary>
    public bool DeleteEdits { get; set; } = true;
}

/// <summary>
///     Types of counting violations.
/// </summary>
public enum CountingViolationType
{
    /// <summary>
    ///     User submitted wrong number.
    /// </summary>
    WrongNumber,

    /// <summary>
    ///     User counted consecutively when not allowed.
    /// </summary>
    ConsecutiveCounting,

    /// <summary>
    ///     User exceeded rate limit.
    /// </summary>
    RateLimit,

    /// <summary>
    ///     Maximum number was reached.
    /// </summary>
    MaxNumberReached,

    /// <summary>
    ///     User used invalid number format.
    /// </summary>
    InvalidFormat,

    /// <summary>
    ///     User lacks required role.
    /// </summary>
    InsufficientPermissions,

    /// <summary>
    ///     User sent a non-number message in counting channel.
    /// </summary>
    NonNumberMessage,

    /// <summary>
    ///     User edited a counting message.
    /// </summary>
    MessageEdit
}

/// <summary>
///     Statistics about violations in a counting channel.
/// </summary>
public class CountingViolationStats
{
    /// <summary>
    ///     Total number of violations.
    /// </summary>
    public int TotalViolations { get; set; }

    /// <summary>
    ///     Number of wrong number violations.
    /// </summary>
    public int WrongNumberCount { get; set; }

    /// <summary>
    ///     Number of timeouts applied.
    /// </summary>
    public int TimeoutCount { get; set; }

    /// <summary>
    ///     Number of bans applied.
    /// </summary>
    public int BanCount { get; set; }

    /// <summary>
    ///     Top violators in the channel.
    /// </summary>
    public List<UserViolationSummary> TopViolators { get; set; } = new();
}

/// <summary>
///     Summary of violations for a specific user.
/// </summary>
public class UserViolationSummary
{
    /// <summary>
    ///     The user's Discord ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Total number of violations by this user.
    /// </summary>
    public int ViolationCount { get; set; }

    /// <summary>
    ///     When the user's last violation occurred.
    /// </summary>
    public DateTime LastViolation { get; set; }
}