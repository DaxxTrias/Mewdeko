using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
/// Represents counting statistics for a user in a specific channel.
/// </summary>
[Table("CountingStats")]
public class CountingStats
{
    /// <summary>
    /// Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    /// The ID of the counting channel.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    /// The ID of the user.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    /// Total number of contributions made by this user.
    /// </summary>
    [Column("ContributionsCount")]
    public long ContributionsCount { get; set; }

    /// <summary>
    /// The highest streak achieved by this user.
    /// </summary>
    [Column("HighestStreak")]
    public int HighestStreak { get; set; }

    /// <summary>
    /// The current active streak for this user.
    /// </summary>
    [Column("CurrentStreak")]
    public int CurrentStreak { get; set; }

    /// <summary>
    /// When this user last contributed to counting.
    /// </summary>
    [Column("LastContribution")]
    public DateTime? LastContribution { get; set; }

    /// <summary>
    /// Total numbers counted by this user (sum of all numbers they've contributed).
    /// </summary>
    [Column("TotalNumbersCounted")]
    public long TotalNumbersCounted { get; set; }

    /// <summary>
    /// Number of errors made by this user.
    /// </summary>
    [Column("ErrorsCount")]
    public int ErrorsCount { get; set; }

    /// <summary>
    /// User's accuracy percentage (correct counts / total attempts * 100).
    /// </summary>
    [Column("Accuracy")]
    public double Accuracy { get; set; }

    /// <summary>
    /// Total time spent counting (in seconds).
    /// </summary>
    [Column("TotalTimeSpent")]
    public long TotalTimeSpent { get; set; }
}

/// <summary>
/// Represents milestone achievements for counting channels.
/// </summary>
[Table("CountingMilestones")]
public class CountingMilestones
{
    /// <summary>
    /// Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    /// The ID of the counting channel.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    /// The milestone number that was reached.
    /// </summary>
    [Column("Number")]
    public long Number { get; set; }

    /// <summary>
    /// When this milestone was reached.
    /// </summary>
    [Column("ReachedAt")]
    public DateTime? ReachedAt { get; set; }

    /// <summary>
    /// The ID of the user who reached this milestone.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    /// Whether a reward was given for this milestone.
    /// </summary>
    [Column("RewardGiven")]
    public bool RewardGiven { get; set; }

    /// <summary>
    /// The type of milestone (every 100, 1000, etc.).
    /// </summary>
    [Column("Type")]
    public int Type { get; set; }
}