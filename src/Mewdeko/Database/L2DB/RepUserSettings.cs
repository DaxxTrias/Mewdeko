using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepUserSettings")]
public class RepUserSettings
{
    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong UserId { get; set; }

    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong GuildId { get; set; }

    [Column("ReceiveDMs")]
    public bool ReceiveDMs { get; set; } = true;

    [Column("DMThreshold")]
    public int DMThreshold { get; set; } = 10;

    [Column("PublicHistory")]
    public bool PublicHistory { get; set; } = true;

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}