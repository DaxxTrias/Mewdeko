using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a Patreon goal cached for a guild
/// </summary>
[Table("PatreonGoals")]
public class PatreonGoal
{
    /// <summary>
    ///     Primary key for the goal record
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Discord guild ID this goal is associated with
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Patreon goal ID from the API
    /// </summary>
    [Column("GoalId")]
    public string GoalId { get; set; } = null!;

    /// <summary>
    ///     Title/name of the goal
    /// </summary>
    [Column("Title")]
    public string Title { get; set; } = null!;

    /// <summary>
    ///     Description of what the goal will achieve
    /// </summary>
    [Column("Description")]
    public string? Description { get; set; }

    /// <summary>
    ///     Target amount in cents for this goal
    /// </summary>
    [Column("AmountCents")]
    public int AmountCents { get; set; }

    /// <summary>
    ///     Percentage of completion (0-100)
    /// </summary>
    [Column("CompletedPercentage")]
    public int CompletedPercentage { get; set; }

    /// <summary>
    ///     Date when the goal was created
    /// </summary>
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Date when the goal was reached (null if not reached)
    /// </summary>
    [Column("ReachedAt")]
    public DateTime? ReachedAt { get; set; }

    /// <summary>
    ///     Whether this goal is currently active
    /// </summary>
    [Column("IsActive")]
    public bool IsActive { get; set; }

    /// <summary>
    ///     Date when this goal record was last updated from the API
    /// </summary>
    [Column("LastUpdated")]
    public DateTime LastUpdated { get; set; }
}