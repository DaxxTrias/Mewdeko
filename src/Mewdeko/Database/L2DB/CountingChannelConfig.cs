using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
/// Represents the configuration settings for a counting channel.
/// </summary>
[Table("CountingChannelConfig")]
public class CountingChannelConfig
{
    /// <summary>
    /// Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    /// The ID of the counting channel this configuration belongs to.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Whether the same user can count consecutively.
    /// </summary>
    [Column("AllowRepeatedUsers")]
    public bool AllowRepeatedUsers { get; set; }

    /// <summary>
    /// Cooldown in seconds between counts from the same user.
    /// </summary>
    [Column("Cooldown")]
    public int Cooldown { get; set; }

    /// <summary>
    /// Comma-separated list of role IDs that can participate in counting.
    /// </summary>
    [Column("RequiredRoles")]
    public string? RequiredRoles { get; set; }

    /// <summary>
    /// Comma-separated list of role IDs that are banned from counting.
    /// </summary>
    [Column("BannedRoles")]
    public string? BannedRoles { get; set; }

    /// <summary>
    /// Maximum number allowed in this channel (0 for unlimited).
    /// </summary>
    [Column("MaxNumber")]
    public long MaxNumber { get; set; }

    /// <summary>
    /// Whether to reset the count when an error occurs.
    /// </summary>
    [Column("ResetOnError")]
    public bool ResetOnError { get; set; }

    /// <summary>
    /// Whether to delete messages with wrong numbers.
    /// </summary>
    [Column("DeleteWrongMessages")]
    public bool DeleteWrongMessages { get; set; }

    /// <summary>
    /// The counting pattern/mode (normal, roman, binary, etc.).
    /// </summary>
    [Column("Pattern")]
    public int Pattern { get; set; }

    /// <summary>
    /// Base for number systems (2-36, default 10).
    /// </summary>
    [Column("NumberBase")]
    public int NumberBase { get; set; }

    /// <summary>
    /// Custom emote to react with on correct counts.
    /// </summary>
    [Column("SuccessEmote")]
    public string? SuccessEmote { get; set; }

    /// <summary>
    /// Custom emote to react with on incorrect counts.
    /// </summary>
    [Column("ErrorEmote")]
    public string? ErrorEmote { get; set; }

    /// <summary>
    /// Whether to enable achievement tracking for this channel.
    /// </summary>
    [Column("EnableAchievements")]
    public bool EnableAchievements { get; set; }

    /// <summary>
    /// Whether to enable competition features for this channel.
    /// </summary>
    [Column("EnableCompetitions")]
    public bool EnableCompetitions { get; set; }
}