using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepChannelConfig")]
public class RepChannelConfig
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("State", CanBeNull = false)]
    public string State { get; set; } = "enabled";

    [Column("Multiplier")]
    public decimal Multiplier { get; set; } = 1.0m;

    [Column("CustomCooldown")]
    public int? CustomCooldown { get; set; }

    [Column("RepType")]
    public string? RepType { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}