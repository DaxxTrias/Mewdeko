using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents punishments applied for counting violations.
/// </summary>
[Table("CountingAppliedPunishments")]
public class CountingAppliedPunishments
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The counting channel ID where the punishment was applied.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The user ID who received the punishment.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The punishment action applied (maps to PunishmentAction enum).
    /// </summary>
    [Column("PunishmentAction")]
    public int PunishmentAction { get; set; }

    /// <summary>
    ///     Duration of the punishment in minutes (0 = permanent).
    /// </summary>
    [Column("DurationMinutes")]
    public int DurationMinutes { get; set; }

    /// <summary>
    ///     Role ID for AddRole punishment type.
    /// </summary>
    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     When this punishment was applied.
    /// </summary>
    [Column("AppliedAt")]
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When this punishment expires (NULL for permanent punishments).
    /// </summary>
    [Column("ExpiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     The wrong count number that triggered this punishment.
    /// </summary>
    [Column("WrongCountAtApplication")]
    public int WrongCountAtApplication { get; set; }

    /// <summary>
    ///     Reason for the punishment.
    /// </summary>
    [Column("Reason")]
    public string? Reason { get; set; }

    /// <summary>
    ///     Whether this punishment is still active.
    /// </summary>
    [Column("IsActive")]
    public bool IsActive { get; set; } = true;
}