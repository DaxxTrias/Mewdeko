using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepWeightedVote")]
public class RepWeightedVote
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("MessageId")]
    public ulong MessageId { get; set; }

    [Column("CreatorId")]
    public ulong CreatorId { get; set; }

    [Column("Title", CanBeNull = false)]
    public string Title { get; set; } = null!;

    [Column("Description", CanBeNull = false)]
    public string Description { get; set; } = null!;

    [Column("OptionsJson", CanBeNull = false)]
    public string OptionsJson { get; set; } = "[]";

    [Column("VoteType", CanBeNull = false)]
    public string VoteType { get; set; } = "single_choice";

    [Column("WeightMethod", CanBeNull = false)]
    public string WeightMethod { get; set; } = "linear";

    [Column("WeightConfigJson")]
    public string? WeightConfigJson { get; set; }

    [Column("MinRepToVote")]
    public int MinRepToVote { get; set; } = 0;

    [Column("MaxWeightPerUser")]
    public int MaxWeightPerUser { get; set; } = 0;

    [Column("ShowLiveResults")]
    public bool ShowLiveResults { get; set; } = true;

    [Column("ShowVoterNames")]
    public bool ShowVoterNames { get; set; } = false;

    [Column("AllowAnonymous")]
    public bool AllowAnonymous { get; set; } = false;

    [Column("StartTime")]
    public DateTime StartTime { get; set; }

    [Column("EndTime")]
    public DateTime EndTime { get; set; }

    [Column("IsClosed")]
    public bool IsClosed { get; set; } = false;

    [Column("RequiredRoles")]
    public string? RequiredRoles { get; set; }

    [Column("CustomRepType")]
    public string? CustomRepType { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}