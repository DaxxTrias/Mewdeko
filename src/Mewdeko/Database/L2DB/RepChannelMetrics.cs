using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepChannelMetrics")]
public class RepChannelMetrics
{
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    [Column("ChannelId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong ChannelId { get; set; }

    [Column("ChannelName", CanBeNull = false)]
    public string ChannelName { get; set; } = null!;

    [Column("TotalRep")]
    public int TotalRep { get; set; }

    [Column("TransactionCount")]
    public int TransactionCount { get; set; }

    [Column("AverageRep")]
    public decimal AverageRep { get; set; }

    [Column("TopUsersJson")]
    public string? TopUsersJson { get; set; }

    [Column("LastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}