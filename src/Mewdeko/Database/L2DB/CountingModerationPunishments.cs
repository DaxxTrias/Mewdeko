using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents tiered punishment configurations for counting violations.
/// </summary>
[Table("CountingModerationPunishments")]
public class CountingModerationPunishments
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild ID this punishment applies to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The channel ID this punishment applies to (NULL for guild defaults).
    /// </summary>
    [Column("ChannelId")]
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     The wrong count number that triggers this punishment.
    /// </summary>
    [Column("Count")]
    public int Count { get; set; }

    /// <summary>
    ///     The punishment action to apply (maps to PunishmentAction enum).
    /// </summary>
    [Column("Punishment")]
    public int Punishment { get; set; }

    /// <summary>
    ///     Duration for timed punishments in minutes (0 = permanent).
    /// </summary>
    [Column("Time")]
    public int Time { get; set; }

    /// <summary>
    ///     Role ID for AddRole punishment type.
    /// </summary>
    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     When this punishment configuration was created.
    /// </summary>
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}