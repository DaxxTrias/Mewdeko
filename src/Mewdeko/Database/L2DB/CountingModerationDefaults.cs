using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents guild-wide default counting moderation configuration.
/// </summary>
[Table("CountingModerationDefaults")]
public class CountingModerationDefaults
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild ID this configuration applies to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether moderation is enabled by default for new counting channels.
    /// </summary>
    [Column("EnableModeration")]
    public bool EnableModeration { get; set; }

    /// <summary>
    ///     Default threshold of wrong counts before punishment is applied.
    /// </summary>
    [Column("WrongCountThreshold")]
    public int WrongCountThreshold { get; set; } = 3;

    /// <summary>
    ///     Default time window in hours for tracking wrong counts.
    /// </summary>
    [Column("TimeWindowHours")]
    public int TimeWindowHours { get; set; } = 24;

    /// <summary>
    ///     Default punishment action to apply (maps to PunishmentAction enum).
    /// </summary>
    [Column("PunishmentAction")]
    public int PunishmentAction { get; set; }

    /// <summary>
    ///     Default duration for timed punishments in minutes (0 = permanent).
    /// </summary>
    [Column("PunishmentDurationMinutes")]
    public int PunishmentDurationMinutes { get; set; }

    /// <summary>
    ///     Default role ID for AddRole punishment type.
    /// </summary>
    [Column("PunishmentRoleId")]
    public ulong? PunishmentRoleId { get; set; }

    /// <summary>
    ///     Comma-separated list of role IDs to ignore from counting by default.
    /// </summary>
    [Column("IgnoreRoles")]
    public string? IgnoreRoles { get; set; }

    /// <summary>
    ///     Whether to delete messages from ignored roles by default.
    /// </summary>
    [Column("DeleteIgnoredMessages")]
    public bool DeleteIgnoredMessages { get; set; }

    /// <summary>
    ///     Comma-separated list of role IDs required to count by default.
    /// </summary>
    [Column("RequiredRoles")]
    public string? RequiredRoles { get; set; }

    /// <summary>
    ///     Comma-separated list of role IDs banned from counting by default.
    /// </summary>
    [Column("BannedRoles")]
    public string? BannedRoles { get; set; }

    /// <summary>
    ///     Whether to punish non-number messages by default.
    /// </summary>
    [Column("PunishNonNumbers")]
    public bool PunishNonNumbers { get; set; }

    /// <summary>
    ///     Whether to delete non-number messages by default.
    /// </summary>
    [Column("DeleteNonNumbers")]
    public bool DeleteNonNumbers { get; set; } = true;

    /// <summary>
    ///     Whether to punish message edits by default.
    /// </summary>
    [Column("PunishEdits")]
    public bool PunishEdits { get; set; }

    /// <summary>
    ///     Whether to delete edited messages by default.
    /// </summary>
    [Column("DeleteEdits")]
    public bool DeleteEdits { get; set; } = true;

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