using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepHourlyActivity")]
public class RepHourlyActivity
{
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    [Column("Date", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public DateTime Date { get; set; }

    [Column("DayOfWeek", IsPrimaryKey = true, PrimaryKeyOrder = 2)]
    public int DayOfWeek { get; set; }

    [Column("Hour", IsPrimaryKey = true, PrimaryKeyOrder = 3)]
    public int Hour { get; set; }

    [Column("TransactionCount")]
    public int TransactionCount { get; set; }

    [Column("TotalRep")]
    public int TotalRep { get; set; }
}