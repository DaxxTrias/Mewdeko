using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepRoleRewards")]
public class RepRoleRewards
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("RoleId")]
    public ulong RoleId { get; set; }

    [Column("RepRequired")]
    public int RepRequired { get; set; }

    [Column("RemoveOnDrop")]
    public bool RemoveOnDrop { get; set; } = true;

    [Column("AnnounceChannel")]
    public ulong? AnnounceChannel { get; set; }

    [Column("AnnounceDM")]
    public bool AnnounceDM { get; set; } = false;

    [Column("XPReward")]
    public int? XPReward { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}