using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents tracking of user wrong counts within time windows.
/// </summary>
[Table("CountingUserWrongCounts")]
public class CountingUserWrongCounts
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The counting channel ID.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The user ID.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Number of wrong counts in this time window.
    /// </summary>
    [Column("WrongCount")]
    public int WrongCount { get; set; } = 1;

    /// <summary>
    ///     When this time window started.
    /// </summary>
    [Column("WindowStartAt")]
    public DateTime WindowStartAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When the last wrong count occurred in this window.
    /// </summary>
    [Column("LastWrongAt")]
    public DateTime LastWrongAt { get; set; } = DateTime.UtcNow;
}