using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepChallengeProgress")]
public class RepChallengeProgress
{
    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong UserId { get; set; }

    [Column("ChallengeId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public int ChallengeId { get; set; }

    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 2)]
    public ulong GuildId { get; set; }

    [Column("Progress")]
    public int Progress { get; set; } = 0;

    [Column("IsCompleted")]
    public bool IsCompleted { get; set; } = false;

    [Column("CompletedAt")]
    public DateTime? CompletedAt { get; set; }

    [Column("RewardsClaimed")]
    public bool RewardsClaimed { get; set; } = false;

    [Column("ProgressData")]
    public string? ProgressData { get; set; }

    [Column("LastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}