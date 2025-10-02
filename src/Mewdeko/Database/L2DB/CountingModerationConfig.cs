using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents per-channel counting moderation configuration that can override guild defaults.
/// </summary>
[Table("CountingModerationConfig")]
public class CountingModerationConfig
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The channel ID this configuration applies to.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Whether to use guild defaults (if false, all override fields must be set).
    /// </summary>
    [Column("UseDefaults")]
    public bool UseDefaults { get; set; } = true;

    /// <summary>
    ///     Override: Whether moderation is enabled for this channel (NULL = use default).
    /// </summary>
    [Column("EnableModeration")]
    public bool? EnableModeration { get; set; }

    /// <summary>
    ///     Override: Threshold of wrong counts before punishment (NULL = use default).
    /// </summary>
    [Column("WrongCountThreshold")]
    public int? WrongCountThreshold { get; set; }

    /// <summary>
    ///     Override: Time window in hours for tracking wrong counts (NULL = use default).
    /// </summary>
    [Column("TimeWindowHours")]
    public int? TimeWindowHours { get; set; }

    /// <summary>
    ///     Override: Punishment action to apply (NULL = use default).
    /// </summary>
    [Column("PunishmentAction")]
    public int? PunishmentAction { get; set; }

    /// <summary>
    ///     Override: Duration for timed punishments in minutes (NULL = use default).
    /// </summary>
    [Column("PunishmentDurationMinutes")]
    public int? PunishmentDurationMinutes { get; set; }

    /// <summary>
    ///     Override: Role ID for AddRole punishment type (NULL = use default).
    /// </summary>
    [Column("PunishmentRoleId")]
    public ulong? PunishmentRoleId { get; set; }

    /// <summary>
    ///     Override: Comma-separated list of role IDs to ignore (NULL = use default).
    /// </summary>
    [Column("IgnoreRoles")]
    public string? IgnoreRoles { get; set; }

    /// <summary>
    ///     Override: Whether to delete ignored messages (NULL = use default).
    /// </summary>
    [Column("DeleteIgnoredMessages")]
    public bool? DeleteIgnoredMessages { get; set; }

    /// <summary>
    ///     Override: Comma-separated list of required role IDs (NULL = use default).
    /// </summary>
    [Column("RequiredRoles")]
    public string? RequiredRoles { get; set; }

    /// <summary>
    ///     Override: Comma-separated list of banned role IDs (NULL = use default).
    /// </summary>
    [Column("BannedRoles")]
    public string? BannedRoles { get; set; }

    /// <summary>
    ///     Override: Whether to punish non-number messages (NULL = use default).
    /// </summary>
    [Column("PunishNonNumbers")]
    public bool? PunishNonNumbers { get; set; }

    /// <summary>
    ///     Override: Whether to delete non-number messages (NULL = use default).
    /// </summary>
    [Column("DeleteNonNumbers")]
    public bool? DeleteNonNumbers { get; set; }

    /// <summary>
    ///     Override: Whether to punish message edits (NULL = use default).
    /// </summary>
    [Column("PunishEdits")]
    public bool? PunishEdits { get; set; }

    /// <summary>
    ///     Override: Whether to delete edited messages (NULL = use default).
    /// </summary>
    [Column("DeleteEdits")]
    public bool? DeleteEdits { get; set; }

    /// <summary>
    ///     When this configuration was created.
    /// </summary>
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When this configuration was last updated.
    /// </summary>
    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}