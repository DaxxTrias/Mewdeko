using Mewdeko.Modules.Counting.Common;

namespace Mewdeko.Controllers.Common.Counting;

/// <summary>
/// Request to setup a counting channel.
/// </summary>
public class SetupCountingChannelRequest
{
    /// <summary>
    /// The number to start counting from.
    /// </summary>
    public long StartNumber { get; set; } = 1;

    /// <summary>
    /// The increment for each count.
    /// </summary>
    public int Increment { get; set; } = 1;
}

/// <summary>
/// Request to update counting channel configuration.
/// </summary>
public class UpdateCountingConfigRequest
{
    /// <summary>
    /// Whether the same user can count consecutively.
    /// </summary>
    public bool? AllowRepeatedUsers { get; set; }

    /// <summary>
    /// Cooldown in seconds between counts from the same user.
    /// </summary>
    public int? Cooldown { get; set; }

    /// <summary>
    /// Comma-separated list of role IDs that can participate in counting.
    /// </summary>
    public string? RequiredRoles { get; set; }

    /// <summary>
    /// Comma-separated list of role IDs that are banned from counting.
    /// </summary>
    public string? BannedRoles { get; set; }

    /// <summary>
    /// Maximum number allowed in this channel (0 for unlimited).
    /// </summary>
    public long? MaxNumber { get; set; }

    /// <summary>
    /// Whether to reset the count when an error occurs.
    /// </summary>
    public bool? ResetOnError { get; set; }

    /// <summary>
    /// Whether to delete messages with wrong numbers.
    /// </summary>
    public bool? DeleteWrongMessages { get; set; }

    /// <summary>
    /// The counting pattern/mode.
    /// </summary>
    public CountingPattern? Pattern { get; set; }

    /// <summary>
    /// Base for number systems (2-36, default 10).
    /// </summary>
    public int? NumberBase { get; set; }

    /// <summary>
    /// Custom emote to react with on correct counts.
    /// </summary>
    public string? SuccessEmote { get; set; }

    /// <summary>
    /// Custom emote to react with on incorrect counts.
    /// </summary>
    public string? ErrorEmote { get; set; }

    /// <summary>
    /// Whether to enable achievement tracking for this channel.
    /// </summary>
    public bool? EnableAchievements { get; set; }

    /// <summary>
    /// Whether to enable competition features for this channel.
    /// </summary>
    public bool? EnableCompetitions { get; set; }
}

/// <summary>
/// Request to reset a counting channel.
/// </summary>
public class ResetCountingChannelRequest
{
    /// <summary>
    /// The number to reset to.
    /// </summary>
    public long NewNumber { get; set; }

    /// <summary>
    /// The ID of the user performing the reset.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Optional reason for the reset.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Request to create a save point.
/// </summary>
public class CreateSavePointRequest
{
    /// <summary>
    /// The ID of the user creating the save point.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Optional reason for creating the save point.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Request to restore from a save point.
/// </summary>
public class RestoreSavePointRequest
{
    /// <summary>
    /// The ID of the save point to restore from.
    /// </summary>
    public int SaveId { get; set; }

    /// <summary>
    /// The ID of the user performing the restore.
    /// </summary>
    public ulong UserId { get; set; }
}