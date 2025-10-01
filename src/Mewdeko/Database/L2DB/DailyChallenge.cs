using LinqToDB.Mapping;
using Mewdeko.Modules.Currency.Services;

namespace DataModel;

/// <summary>
///     Represents a daily challenge for currency games.
/// </summary>
[Table("DailyChallenges")]
public class DailyChallenge
{
    /// <summary>
    ///     Gets or sets the ID.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the date of the challenge.
    /// </summary>
    [Column("Date")]
    public DateTime Date { get; set; }

    /// <summary>
    ///     Gets or sets the type of challenge.
    /// </summary>
    [Column("ChallengeType")]
    public DailyChallengeType ChallengeType { get; set; }

    /// <summary>
    ///     Gets or sets the challenge description.
    /// </summary>
    [Column("Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the required amount to complete the challenge.
    /// </summary>
    [Column("RequiredAmount")]
    public int RequiredAmount { get; set; }

    /// <summary>
    ///     Gets or sets the current progress.
    /// </summary>
    [Column("Progress")]
    public int Progress { get; set; }

    /// <summary>
    ///     Gets or sets the reward amount.
    /// </summary>
    [Column("RewardAmount")]
    public long RewardAmount { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the challenge is completed.
    /// </summary>
    [Column("IsCompleted")]
    public bool IsCompleted { get; set; }

    /// <summary>
    ///     Gets or sets the completion time.
    /// </summary>
    [Column("CompletedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     Gets or sets the date added.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}