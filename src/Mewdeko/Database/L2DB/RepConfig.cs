using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepConfig")]
public class RepConfig
{
    [Column("GuildId", IsPrimaryKey = true)]
    public ulong GuildId { get; set; }

    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    [Column("DefaultCooldownMinutes")]
    public int DefaultCooldownMinutes { get; set; } = 60;

    [Column("DailyLimit")]
    public int DailyLimit { get; set; } = 10;

    [Column("WeeklyLimit")]
    public int? WeeklyLimit { get; set; }

    [Column("MinAccountAgeDays")]
    public int MinAccountAgeDays { get; set; } = 7;

    [Column("MinServerMembershipHours")]
    public int MinServerMembershipHours { get; set; } = 24;

    [Column("MinMessageCount")]
    public int MinMessageCount { get; set; } = 10;

    [Column("EnableNegativeRep")]
    public bool EnableNegativeRep { get; set; } = false;

    [Column("EnableAnonymous")]
    public bool EnableAnonymous { get; set; } = false;

    [Column("EnableDecay")]
    public bool EnableDecay { get; set; } = false;

    [Column("DecayType", CanBeNull = false)]
    public string DecayType { get; set; } = "weekly";

    [Column("DecayAmount")]
    public int DecayAmount { get; set; } = 1;

    [Column("DecayInactiveDays")]
    public int DecayInactiveDays { get; set; } = 30;

    [Column("NotificationChannel")]
    public ulong? NotificationChannel { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}