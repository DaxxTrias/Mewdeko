using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepChallenge")]
public class RepChallenge
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Name", CanBeNull = false)]
    public string Name { get; set; } = null!;

    [Column("Description", CanBeNull = false)]
    public string Description { get; set; } = null!;

    [Column("ChallengeType", CanBeNull = false)]
    public string ChallengeType { get; set; } = "weekly";

    [Column("GoalType", CanBeNull = false)]
    public string GoalType { get; set; } = null!;

    [Column("TargetValue")]
    public int TargetValue { get; set; }

    [Column("RepReward")]
    public int RepReward { get; set; }

    [Column("XpReward")]
    public int? XpReward { get; set; }

    [Column("BadgeReward")]
    public string? BadgeReward { get; set; }

    [Column("RoleReward")]
    public ulong? RoleReward { get; set; }

    [Column("StartTime")]
    public DateTime StartTime { get; set; }

    [Column("EndTime")]
    public DateTime EndTime { get; set; }

    [Column("IsServerWide")]
    public bool IsServerWide { get; set; } = false;

    [Column("ServerWideTarget")]
    public int? ServerWideTarget { get; set; }

    [Column("MinParticipants")]
    public int? MinParticipants { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("AnnounceProgress")]
    public bool AnnounceProgress { get; set; } = true;

    [Column("AnnounceChannel")]
    public ulong? AnnounceChannel { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}