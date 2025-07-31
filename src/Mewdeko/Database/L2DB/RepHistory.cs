using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepHistory")]
public class RepHistory
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GiverId")]
    public ulong GiverId { get; set; }

    [Column("ReceiverId")]
    public ulong ReceiverId { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("Amount")]
    public int Amount { get; set; }

    [Column("RepType", CanBeNull = false)]
    public string RepType { get; set; } = null!;

    [Column("Reason")]
    public string? Reason { get; set; }

    [Column("IsAnonymous")]
    public bool IsAnonymous { get; set; }

    [Column("Timestamp")]
    public DateTime Timestamp { get; set; }
}