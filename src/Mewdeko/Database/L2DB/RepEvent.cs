using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepEvent")]
public class RepEvent
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Name", CanBeNull = false)]
    public string Name { get; set; } = null!;

    [Column("Description")]
    public string? Description { get; set; }

    [Column("EventType", CanBeNull = false)]
    public string EventType { get; set; } = "custom";

    [Column("StartTime")]
    public DateTime StartTime { get; set; }

    [Column("EndTime")]
    public DateTime EndTime { get; set; }

    [Column("Multiplier")]
    public decimal Multiplier { get; set; } = 2.0m;

    [Column("BonusAmount")]
    public int BonusAmount { get; set; } = 0;

    [Column("RestrictedChannels")]
    public string? RestrictedChannels { get; set; }

    [Column("RestrictedRoles")]
    public string? RestrictedRoles { get; set; }

    [Column("IsRecurring")]
    public bool IsRecurring { get; set; } = false;

    [Column("RecurrencePattern")]
    public string? RecurrencePattern { get; set; }

    [Column("ActiveMessage")]
    public string? ActiveMessage { get; set; }

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("EventBadge")]
    public string? EventBadge { get; set; }

    [Column("IsAnnounced")]
    public bool IsAnnounced { get; set; } = false;

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}