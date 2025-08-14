using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
/// Represents a counting channel configuration in a guild.
/// </summary>
[Table("CountingChannel")]
public class CountingChannel
{
    /// <summary>
    /// Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    /// The ID of the guild this counting channel belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    /// The ID of the Discord channel used for counting.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    /// The current number in the counting sequence.
    /// </summary>
    [Column("CurrentNumber")]
    public long CurrentNumber { get; set; }

    /// <summary>
    /// The number the counting started from.
    /// </summary>
    [Column("StartNumber")]
    public long StartNumber { get; set; }

    /// <summary>
    /// The increment value for each count (usually 1).
    /// </summary>
    [Column("Increment")]
    public int Increment { get; set; }

    /// <summary>
    /// The ID of the user who made the last valid count.
    /// </summary>
    [Column("LastUserId")]
    public ulong LastUserId { get; set; }

    /// <summary>
    /// The ID of the last valid counting message.
    /// </summary>
    [Column("LastMessageId")]
    public ulong LastMessageId { get; set; }

    /// <summary>
    /// Whether this counting channel is currently active.
    /// </summary>
    [Column("IsActive")]
    public bool IsActive { get; set; }

    /// <summary>
    /// When this counting channel was created.
    /// </summary>
    [Column("CreatedAt")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// The highest number reached in this channel.
    /// </summary>
    [Column("HighestNumber")]
    public long HighestNumber { get; set; }

    /// <summary>
    /// When the highest number was reached.
    /// </summary>
    [Column("HighestNumberReachedAt")]
    public DateTime? HighestNumberReachedAt { get; set; }

    /// <summary>
    /// Total number of valid counts made in this channel.
    /// </summary>
    [Column("TotalCounts")]
    public long TotalCounts { get; set; }
}