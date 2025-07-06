using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
/// Represents a poll scheduled to be created at a future time.
/// </summary>
[Table("scheduled_polls")]
public class ScheduledPoll
{
    /// <summary>
    /// Gets or sets the unique identifier for this scheduled poll.
    /// </summary>
    [Column("id"), PrimaryKey, Identity]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID where the poll will be created.
    /// </summary>
    [Column("guild_id")]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel ID where the poll will be posted.
    /// </summary>
    [Column("channel_id")]
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user ID of who scheduled the poll.
    /// </summary>
    [Column("creator_id")]
    public ulong CreatorId { get; set; }

    /// <summary>
    /// Gets or sets the poll question.
    /// </summary>
    [Column("question")]
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the poll options as JSON.
    /// </summary>
    [Column("options")]
    public string Options { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the poll type.
    /// </summary>
    [Column("poll_type")]
    public int Type { get; set; }

    /// <summary>
    /// Gets or sets the poll settings as JSON.
    /// </summary>
    [Column("settings")]
    public string? Settings { get; set; }

    /// <summary>
    /// Gets or sets when the poll should be created.
    /// </summary>
    [Column("scheduled_for")]
    public DateTime ScheduledFor { get; set; }

    /// <summary>
    /// Gets or sets the poll duration in minutes before auto-close.
    /// </summary>
    [Column("duration_minutes")]
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets when this poll was scheduled.
    /// </summary>
    [Column("scheduled_at")]
    public DateTime ScheduledAt { get; set; }

    /// <summary>
    /// Gets or sets whether this scheduled poll has been executed.
    /// </summary>
    [Column("is_executed")]
    public bool IsExecuted { get; set; }

    /// <summary>
    /// Gets or sets when the poll was actually created (if executed).
    /// </summary>
    [Column("executed_at")]
    public DateTime? ExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the ID of the created poll (if executed).
    /// </summary>
    [Column("created_poll_id")]
    public int? CreatedPollId { get; set; }

    /// <summary>
    /// Gets or sets whether this scheduled poll was cancelled.
    /// </summary>
    [Column("is_cancelled")]
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Gets or sets when the scheduled poll was cancelled.
    /// </summary>
    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Gets or sets who cancelled the scheduled poll.
    /// </summary>
    [Column("cancelled_by")]
    public ulong? CancelledBy { get; set; }
}