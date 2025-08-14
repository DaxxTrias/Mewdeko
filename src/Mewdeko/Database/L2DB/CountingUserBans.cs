using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a ban for a user from counting in a specific channel.
/// </summary>
[Table("CountingUserBans")]
public class CountingUserBans
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The ID of the counting channel.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The ID of the banned user.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The ID of the user who issued the ban.
    /// </summary>
    [Column("BannedBy")]
    public ulong BannedBy { get; set; }

    /// <summary>
    ///     When the ban was issued.
    /// </summary>
    [Column("BannedAt")]
    public DateTime BannedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When the ban expires (null for permanent bans).
    /// </summary>
    [Column("ExpiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     The reason for the ban.
    /// </summary>
    [Column("Reason")]
    public string? Reason { get; set; }

    /// <summary>
    ///     Whether this is an active ban.
    /// </summary>
    [Column("IsActive")]
    public bool IsActive { get; set; } = true;
}