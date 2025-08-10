using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("UserReputation")]
public class UserReputation
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("TotalRep")]
    public int TotalRep { get; set; }

    [Column("HelperRep")]
    public int? HelperRep { get; set; }

    [Column("ArtistRep")]
    public int? ArtistRep { get; set; }

    [Column("MemerRep")]
    public int? MemerRep { get; set; }

    [Column("LastGivenAt")]
    public DateTime? LastGivenAt { get; set; }

    [Column("LastReceivedAt")]
    public DateTime? LastReceivedAt { get; set; }

    [Column("CurrentStreak")]
    public int CurrentStreak { get; set; }

    [Column("LongestStreak")]
    public int LongestStreak { get; set; }

    [Column("IsFrozen")]
    public bool IsFrozen { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}