using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
/// Represents events that occur in counting channels for audit trail.
/// </summary>
[Table("CountingEvents")]
public class CountingEvents
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
    /// The type of event that occurred.
    /// </summary>
    [Column("EventType")]
    public int EventType { get; set; }

    /// <summary>
    /// The ID of the user who triggered the event.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    /// The previous number before the event.
    /// </summary>
    [Column("OldNumber")]
    public long? OldNumber { get; set; }

    /// <summary>
    /// The new number after the event.
    /// </summary>
    [Column("NewNumber")]
    public long? NewNumber { get; set; }

    /// <summary>
    /// When this event occurred.
    /// </summary>
    [Column("Timestamp")]
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// The Discord message ID associated with this event.
    /// </summary>
    [Column("MessageId")]
    public ulong? MessageId { get; set; }

    /// <summary>
    /// Additional details about the event.
    /// </summary>
    [Column("Details")]
    public string? Details { get; set; }
}

/// <summary>
/// Represents save points for counting channels.
/// </summary>
[Table("CountingSaves")]
public class CountingSaves
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
    /// The saved number at this save point.
    /// </summary>
    [Column("SavedNumber")]
    public long SavedNumber { get; set; }

    /// <summary>
    /// When this save point was created.
    /// </summary>
    [Column("SavedAt")]
    public DateTime? SavedAt { get; set; }

    /// <summary>
    /// The ID of the user who created this save point.
    /// </summary>
    [Column("SavedBy")]
    public ulong SavedBy { get; set; }

    /// <summary>
    /// The reason for creating this save point.
    /// </summary>
    [Column("Reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Whether this save point is still available for restore.
    /// </summary>
    [Column("IsActive")]
    public bool IsActive { get; set; }
}

/// <summary>
/// Represents leaderboard entries for counting channels.
/// </summary>
[Table("CountingLeaderboard")]
public class CountingLeaderboard
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
    /// The user's calculated score for ranking.
    /// </summary>
    [Column("Score")]
    public long Score { get; set; }

    /// <summary>
    /// The user's current rank in this channel.
    /// </summary>
    [Column("Rank")]
    public int Rank { get; set; }

    /// <summary>
    /// When this leaderboard entry was last updated.
    /// </summary>
    [Column("LastUpdated")]
    public DateTime? LastUpdated { get; set; }
}