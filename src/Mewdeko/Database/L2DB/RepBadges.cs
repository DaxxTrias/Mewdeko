using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepBadges")]
public class RepBadges
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("BadgeType", CanBeNull = false)]
    public string BadgeType { get; set; } = null!;

    [Column("EarnedAt")]
    public DateTime EarnedAt { get; set; }
}