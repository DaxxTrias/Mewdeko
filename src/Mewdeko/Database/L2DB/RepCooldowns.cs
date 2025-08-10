using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepCooldowns")]
public class RepCooldowns
{
    [Column("GiverId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GiverId { get; set; }

    [Column("ReceiverId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong ReceiverId { get; set; }

    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 2)]
    public ulong GuildId { get; set; }

    [Column("ExpiresAt")]
    public DateTime ExpiresAt { get; set; }
}