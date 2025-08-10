using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepMetrics")]
public class RepMetrics
{
    [Column("GuildId", IsPrimaryKey = true)]
    public ulong GuildId { get; set; }

    [Column("TotalRepGiven")]
    public long TotalRepGiven { get; set; }

    [Column("UniqueGivers")]
    public int UniqueGivers { get; set; }

    [Column("UniqueReceivers")]
    public int UniqueReceivers { get; set; }

    [Column("AverageRepPerUser")]
    public decimal AverageRepPerUser { get; set; }

    [Column("PeakHour")]
    public int PeakHour { get; set; }

    [Column("PeakDayOfWeek")]
    public int PeakDayOfWeek { get; set; }

    [Column("ChannelActivityJson")]
    public string? ChannelActivityJson { get; set; }

    [Column("RepTypeDistributionJson")]
    public string? RepTypeDistributionJson { get; set; }

    [Column("RetentionRate")]
    public decimal RetentionRate { get; set; }

    [Column("StartDate")]
    public DateTime StartDate { get; set; }

    [Column("EndDate")]
    public DateTime EndDate { get; set; }

    [Column("LastCalculated")]
    public DateTime LastCalculated { get; set; } = DateTime.UtcNow;
}