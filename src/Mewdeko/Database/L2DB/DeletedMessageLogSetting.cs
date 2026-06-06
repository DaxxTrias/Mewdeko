using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("DeletedMessageLogSettings")]
public class DeletedMessageLogSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong? ChannelId { get; set; }

    [Column("Enabled")]
    public bool Enabled { get; set; }

    [Column("MaxAgeMinutes")]
    public int MaxAgeMinutes { get; set; } = 10;

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    [Column("DateModified")]
    public DateTime? DateModified { get; set; }
}
