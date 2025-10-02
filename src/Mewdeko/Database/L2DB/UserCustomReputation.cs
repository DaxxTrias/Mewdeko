using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("UserCustomReputation")]
public class UserCustomReputation
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("CustomTypeId")]
    public int CustomTypeId { get; set; }

    [Column("Amount")]
    public int Amount { get; set; }

    [Column("LastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}